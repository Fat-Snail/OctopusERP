using Microsoft.EntityFrameworkCore;
using OctopusPLM.Api.DTOs;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Services;

public class CategoryService
{
    private readonly PlmDbContext _db;

    public CategoryService(PlmDbContext db) => _db = db;

    /// <summary>递归构建类目树</summary>
    public async Task<List<CategoryTreeNode>> GetTreeAsync()
    {
        var all = await _db.Categories.OrderBy(c => c.OrderNum).ToListAsync();
        return BuildTree(all, null);
    }

    private static List<CategoryTreeNode> BuildTree(List<Core.Entities.Category> all, long? parentId)
    {
        return all.Where(c => c.ParentId == parentId).Select(c => new CategoryTreeNode
        {
            CategoryId = c.CategoryId,
            ParentId = c.ParentId,
            Name = c.Name,
            Level = c.Level,
            OrderNum = c.OrderNum,
            Children = BuildTree(all, c.CategoryId),
        }).ToList();
    }

    /// <summary>获取类目下绑定的属性（含枚举值），按 GroupName 分组</summary>
    public async Task<List<CategoryAttributeGrouped>> GetAttributesAsync(long categoryId)
    {
        var activeModelVersionId = await GetActiveModelVersionIdAsync(categoryId);
        return await GetGroupedAttributesAsync(categoryId, activeModelVersionId);
    }

    public async Task<List<CategoryModelVersionSummary>> GetModelVersionsAsync(long categoryId)
    {
        return await _db.CategoryModelVersions
            .Where(v => v.CategoryId == categoryId)
            .OrderByDescending(v => v.EffectiveFrom)
            .Select(v => new CategoryModelVersionSummary
            {
                ModelVersionId = v.ModelVersionId,
                VersionNo = v.VersionNo,
                Status = v.Status,
                EffectiveFrom = v.EffectiveFrom,
                PublishedAt = v.PublishedAt,
                PublishedBy = v.PublishedBy,
                ChangeSummary = v.ChangeSummary,
            })
            .ToListAsync();
    }

    public async Task<CategoryModelDetailResponse?> GetModelAsync(long categoryId)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category == null) return null;

        var versions = await GetModelVersionsAsync(categoryId);
        var activeVersion = versions.FirstOrDefault(v => v.Status == "active") ?? versions.FirstOrDefault();
        var activeVersionId = activeVersion?.ModelVersionId;

        var rules = activeVersionId == null
            ? new List<CategoryRuleResponse>()
            : await _db.RuleDefs
                .Where(r => r.ModelVersionId == activeVersionId)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.RuleId)
                .Select(r => new CategoryRuleResponse
                {
                    RuleId = r.RuleId,
                    RuleType = r.RuleType,
                    RuleCode = r.RuleCode,
                    RuleName = r.RuleName,
                    TriggerExpr = r.TriggerExpr,
                    ActionExpr = r.ActionExpr,
                    Priority = r.Priority,
                    Status = r.Status,
                    Message = r.Message,
                })
                .ToListAsync();

        var templates = activeVersionId == null
            ? new List<DetailTemplateResponse>()
            : await _db.DetailTemplates
                .Where(t => t.ModelVersionId == activeVersionId)
                .Include(t => t.ComponentBinds.OrderBy(b => b.OrderNum))
                .ThenInclude(b => b.Component)
                .OrderBy(t => t.TemplateId)
                .Select(t => new DetailTemplateResponse
                {
                    TemplateId = t.TemplateId,
                    TemplateName = t.TemplateName,
                    Status = t.Status,
                    Components = t.ComponentBinds
                        .OrderBy(b => b.OrderNum)
                        .Select(b => new DetailComponentItem
                        {
                            BindId = b.BindId,
                            ComponentId = b.ComponentId,
                            ComponentCode = b.Component!.ComponentCode,
                            ComponentName = b.Component.ComponentName,
                            ComponentType = b.Component.ComponentType,
                            OrderNum = b.OrderNum,
                            IsRequired = b.IsRequired,
                            DisplayRuleJson = b.DisplayRuleJson,
                            DefaultContentJson = b.DefaultContentJson,
                        })
                        .ToList(),
                })
                .ToListAsync();

        var channelMappings = await _db.ChannelCategoryMappings
            .Where(m => m.CategoryId == categoryId)
            .Include(m => m.Channel)
            .Include(m => m.AttributeMappings)
            .ThenInclude(m => m.Attribute)
            .OrderBy(m => m.ChannelId)
            .Select(m => new ChannelCategoryMappingResponse
            {
                MappingId = m.MappingId,
                ChannelCode = m.Channel!.ChannelCode,
                ChannelName = m.Channel.ChannelName,
                ExternalCategoryId = m.ExternalCategoryId,
                ExternalCategoryName = m.ExternalCategoryName,
                MappingVersion = m.MappingVersion,
                AttributeMappings = m.AttributeMappings
                    .OrderBy(a => a.MappingId)
                    .Select(a => new ChannelAttributeMappingResponse
                    {
                        MappingId = a.MappingId,
                        AttributeId = a.AttributeId,
                        AttributeName = a.Attribute!.Name,
                        AttributeCode = a.Attribute.Code,
                        ExternalAttributeId = a.ExternalAttributeId,
                        ExternalAttributeName = a.ExternalAttributeName,
                        IsRequiredOutbound = a.IsRequiredOutbound,
                        TransformRuleJson = a.TransformRuleJson,
                    })
                    .ToList(),
            })
            .ToListAsync();

        return new CategoryModelDetailResponse
        {
            CategoryId = category.CategoryId,
            CategoryName = category.Name,
            ActiveVersion = activeVersion,
            Versions = versions,
            AttributeGroups = await GetGroupedAttributesAsync(categoryId, activeVersionId),
            Rules = rules,
            DetailTemplates = templates,
            ChannelMappings = channelMappings,
        };
    }

    private async Task<long?> GetActiveModelVersionIdAsync(long categoryId)
    {
        return await _db.CategoryModelVersions
            .Where(v => v.CategoryId == categoryId && v.Status == "active")
            .OrderByDescending(v => v.EffectiveFrom)
            .Select(v => (long?)v.ModelVersionId)
            .FirstOrDefaultAsync();
    }

    private async Task<List<CategoryAttributeGrouped>> GetGroupedAttributesAsync(long categoryId, long? modelVersionId)
    {
        var bindings = await _db.CategoryAttributes
            .Where(ca => ca.CategoryId == categoryId && (modelVersionId == null
                ? ca.ModelVersionId == null
                : ca.ModelVersionId == modelVersionId))
            .OrderBy(ca => ca.OrderNum)
            .Include(ca => ca.Attribute)
            .ThenInclude(a => a!.Values)
            .ToListAsync();

        var list = bindings.Select(ca => new CategoryAttributeResponse
        {
            Id = ca.Id,
            AttributeId = ca.AttributeId,
            Code = ca.Attribute?.Code,
            Name = ca.Attribute?.Name ?? "",
            AttributeType = ca.Attribute?.AttributeType ?? "text",
            InputType = ca.Attribute?.InputType ?? "single_line",
            Unit = ca.Attribute?.Unit,
            IsRequired = ca.IsRequired,
            OrderNum = ca.OrderNum,
            GroupName = ca.GroupName,
            GroupType = ca.GroupType,
            IsSaleAxis = ca.IsSaleAxis,
            ExtRulesJson = ca.ExtRulesJson,
            Values = (ca.Attribute?.Values ?? new())
                .Where(v => v.Status == 1)
                .OrderBy(v => v.OrderNum)
                .Select(v => new AttributeValueItem { ValueId = v.ValueId, Label = v.Label, Value = v.Value })
                .ToList(),
        }).ToList();

        var grouped = new List<CategoryAttributeGrouped>();
        var defaultGroup = new CategoryAttributeGrouped
        {
            GroupName = null,
            GroupLabel = "基础属性",
            Attributes = list.Where(a => string.IsNullOrEmpty(a.GroupName)).ToList(),
        };
        if (defaultGroup.Attributes.Count > 0) grouped.Add(defaultGroup);

        foreach (var g in list.Where(a => !string.IsNullOrEmpty(a.GroupName)).GroupBy(a => a.GroupName))
        {
            grouped.Add(new CategoryAttributeGrouped
            {
                GroupName = g.Key,
                GroupLabel = g.Key ?? "",
                Attributes = g.ToList(),
            });
        }

        return grouped;
    }

    /// <summary>创建类目</summary>
    public async Task<(Core.Entities.Category? Cat, string? Error)> CreateCategoryAsync(string name, long? parentId, int orderNum)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, "类目名称不能为空");
        var exists = await _db.Categories.AnyAsync(c => c.Name == name && c.ParentId == parentId);
        if (exists) return (null, "同级下已存在同名类目");

        int level = 1;
        if (parentId.HasValue)
        {
            var parent = await _db.Categories.FindAsync(parentId.Value);
            if (parent == null) return (null, "父类目不存在");
            level = parent.Level + 1;
        }

        var cat = new Core.Entities.Category
        {
            Name = name,
            ParentId = parentId,
            Level = level,
            OrderNum = orderNum,
            Status = 1,
            CreateTime = DateTime.UtcNow,
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return (cat, null);
    }

    /// <summary>更新类目</summary>
    public async Task<string?> UpdateCategoryAsync(long id, string name, int orderNum)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return "类目不存在";
        var dup = await _db.Categories.AnyAsync(c => c.Name == name && c.ParentId == cat.ParentId && c.CategoryId != id);
        if (dup) return "同级下已存在同名类目";

        cat.Name = name;
        cat.OrderNum = orderNum;
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>删除类目（无子节点且无关联商品才允许）</summary>
    public async Task<string?> DeleteCategoryAsync(long id)
    {
        var hasChildren = await _db.Categories.AnyAsync(c => c.ParentId == id);
        if (hasChildren) return "该类目下有子类目，请先删除子类目";
        var hasProducts = await _db.Products.AnyAsync(p => p.CategoryId == id);
        if (hasProducts) return "该类目下有商品，无法删除";

        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return "类目不存在";
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return null;
    }

    // ── 属性 CRUD ──────────────────────────────────────────────────────────

    public async Task<List<Core.Entities.PlmAttribute>> GetAllAttributesAsync()
    {
        return await _db.Attributes.Include(a => a.Values).OrderBy(a => a.Name).ToListAsync();
    }

    public async Task<(Core.Entities.PlmAttribute? Attr, string? Error)> CreateAttributeAsync(
        string name, string? code, string attributeType, string inputType, string? unit, string valueScope)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, "属性名称不能为空");
        if (!string.IsNullOrEmpty(code))
        {
            var codeExists = await _db.Attributes.AnyAsync(a => a.Code == code);
            if (codeExists) return (null, "属性编码已存在");
        }

        var attr = new Core.Entities.PlmAttribute
        {
            Name = name,
            Code = string.IsNullOrWhiteSpace(code) ? null : code,
            AttributeType = attributeType,
            InputType = inputType,
            Unit = unit,
            ValueScope = valueScope,
            Status = 1,
        };
        _db.Attributes.Add(attr);
        await _db.SaveChangesAsync();
        return (attr, null);
    }

    public async Task<string?> UpdateAttributeAsync(long id, string name, string? unit)
    {
        var attr = await _db.Attributes.FindAsync(id);
        if (attr == null) return "属性不存在";
        attr.Name = name;
        attr.Unit = unit;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteAttributeAsync(long id)
    {
        var inUse = await _db.CategoryAttributes.AnyAsync(ca => ca.AttributeId == id);
        if (inUse) return "该属性已被类目绑定，无法删除";
        var attr = await _db.Attributes.FindAsync(id);
        if (attr == null) return "属性不存在";
        _db.Attributes.Remove(attr);
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<(Core.Entities.AttributeValue? Val, string? Error)> AddAttributeValueAsync(long attributeId, string label, string value, int orderNum)
    {
        var attr = await _db.Attributes.FindAsync(attributeId);
        if (attr == null) return (null, "属性不存在");
        var dup = await _db.AttributeValues.AnyAsync(v => v.AttributeId == attributeId && v.Value == value);
        if (dup) return (null, "该属性值已存在");

        var val = new Core.Entities.AttributeValue
        {
            AttributeId = attributeId,
            Label = label,
            Value = value,
            OrderNum = orderNum,
            Status = 1,
        };
        _db.AttributeValues.Add(val);
        await _db.SaveChangesAsync();
        return (val, null);
    }

    public async Task<string?> DeleteAttributeValueAsync(long valueId)
    {
        var val = await _db.AttributeValues.FindAsync(valueId);
        if (val == null) return "属性值不存在";
        _db.AttributeValues.Remove(val);
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>获取指定版本的属性（按 GroupName 分组）</summary>
    public async Task<List<CategoryAttributeGrouped>> GetVersionAttributesAsync(long categoryId, long versionId)
        => await GetGroupedAttributesAsync(categoryId, versionId);

    /// <summary>新建草稿版本，克隆 active 版本的属性绑定</summary>
    public async Task<(Core.Entities.CategoryModelVersion? Version, string? Error)> CreateModelVersionAsync(long categoryId, string? changeSummary)
    {
        if (await _db.Categories.FindAsync(categoryId) == null) return (null, "类目不存在");

        var draftExists = await _db.CategoryModelVersions.AnyAsync(v => v.CategoryId == categoryId && v.Status == "draft");
        if (draftExists) return (null, "已有草稿版本，请先发布或删除当前草稿");

        var activeVersion = await _db.CategoryModelVersions
            .Include(v => v.AttributeBinds)
            .FirstOrDefaultAsync(v => v.CategoryId == categoryId && v.Status == "active");

        var latest = await _db.CategoryModelVersions
            .Where(v => v.CategoryId == categoryId)
            .OrderByDescending(v => v.ModelVersionId)
            .FirstOrDefaultAsync();

        var versionNo = latest == null ? "v1" : IncrementVersionNo(latest.VersionNo);

        var newVersion = new Core.Entities.CategoryModelVersion
        {
            CategoryId = categoryId,
            VersionNo = versionNo,
            Status = "draft",
            EffectiveFrom = DateTime.UtcNow,
            ChangeSummary = changeSummary,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.CategoryModelVersions.Add(newVersion);
        await _db.SaveChangesAsync();

        if (activeVersion?.AttributeBinds != null)
        {
            foreach (var bind in activeVersion.AttributeBinds)
            {
                _db.CategoryAttributes.Add(new Core.Entities.CategoryAttribute
                {
                    CategoryId = bind.CategoryId,
                    AttributeId = bind.AttributeId,
                    ModelVersionId = newVersion.ModelVersionId,
                    IsRequired = bind.IsRequired,
                    IsSaleAxis = bind.IsSaleAxis,
                    GroupType = bind.GroupType,
                    GroupName = bind.GroupName,
                    OrderNum = bind.OrderNum,
                });
            }
            await _db.SaveChangesAsync();
        }

        return (newVersion, null);
    }

    private static string IncrementVersionNo(string versionNo)
    {
        if (versionNo.StartsWith('v') && int.TryParse(versionNo[1..], out var n)) return $"v{n + 1}";
        return versionNo + "-2";
    }

    /// <summary>更新草稿版本变更说明</summary>
    public async Task<string?> UpdateModelVersionAsync(long categoryId, long versionId, string? changeSummary)
    {
        var version = await _db.CategoryModelVersions.FindAsync(versionId);
        if (version == null || version.CategoryId != categoryId) return "版本不存在";
        if (version.Status != "draft") return "只能修改草稿版本";
        version.ChangeSummary = changeSummary;
        version.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>发布草稿版本，归档旧 active 版</summary>
    public async Task<string?> PublishModelVersionAsync(long categoryId, long versionId)
    {
        var version = await _db.CategoryModelVersions.FindAsync(versionId);
        if (version == null || version.CategoryId != categoryId) return "版本不存在";
        if (version.Status != "draft") return "只有草稿版本可以发布";

        var activeVersions = await _db.CategoryModelVersions
            .Where(v => v.CategoryId == categoryId && v.Status == "active")
            .ToListAsync();
        foreach (var av in activeVersions) av.Status = "archived";

        version.Status = "active";
        version.PublishedAt = DateTime.UtcNow;
        version.EffectiveFrom = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>删除草稿版本（含其所有属性绑定）</summary>
    public async Task<string?> DeleteModelVersionAsync(long categoryId, long versionId)
    {
        var version = await _db.CategoryModelVersions
            .Include(v => v.AttributeBinds)
            .FirstOrDefaultAsync(v => v.ModelVersionId == versionId && v.CategoryId == categoryId);
        if (version == null) return "版本不存在";
        if (version.Status != "draft") return "只能删除草稿版本";

        _db.CategoryAttributes.RemoveRange(version.AttributeBinds);
        _db.CategoryModelVersions.Remove(version);
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>向草稿版本绑定属性</summary>
    public async Task<(Core.Entities.CategoryAttribute? Binding, string? Error)> BindAttributeToVersionAsync(
        long categoryId, long versionId, long attributeId, bool isRequired, bool isSaleAxis, string groupType, string? groupName, int orderNum)
    {
        var version = await _db.CategoryModelVersions.FindAsync(versionId);
        if (version == null || version.CategoryId != categoryId) return (null, "版本不存在");
        if (version.Status != "draft") return (null, "只能修改草稿版本的属性");
        if (await _db.Attributes.FindAsync(attributeId) == null) return (null, "属性不存在");

        var exists = await _db.CategoryAttributes.AnyAsync(ca =>
            ca.CategoryId == categoryId && ca.AttributeId == attributeId && ca.ModelVersionId == versionId);
        if (exists) return (null, "该属性已绑定到此版本");

        var binding = new Core.Entities.CategoryAttribute
        {
            CategoryId = categoryId,
            AttributeId = attributeId,
            ModelVersionId = versionId,
            IsRequired = isRequired,
            IsSaleAxis = isSaleAxis,
            GroupType = groupType,
            GroupName = groupName,
            OrderNum = orderNum,
        };
        _db.CategoryAttributes.Add(binding);
        await _db.SaveChangesAsync();
        return (binding, null);
    }

    /// <summary>从草稿版本解绑属性</summary>
    public async Task<string?> UnbindAttributeFromVersionAsync(long categoryId, long versionId, long bindId)
    {
        var version = await _db.CategoryModelVersions.FindAsync(versionId);
        if (version == null || version.CategoryId != categoryId) return "版本不存在";
        if (version.Status != "draft") return "只能修改草稿版本的属性";

        var binding = await _db.CategoryAttributes.FindAsync(bindId);
        if (binding == null || binding.CategoryId != categoryId || binding.ModelVersionId != versionId) return "绑定记录不存在";

        _db.CategoryAttributes.Remove(binding);
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>绑定属性到类目（关联到当前 active 版本，不存在则自动创建初始版本）</summary>
    public async Task<(Core.Entities.CategoryAttribute? Binding, string? Error)> BindAttributeAsync(
        long categoryId, long attributeId, bool isRequired, bool isSaleAxis, string groupType, string? groupName, int orderNum)
    {
        if (await _db.Categories.FindAsync(categoryId) == null) return (null, "类目不存在");
        if (await _db.Attributes.FindAsync(attributeId) == null) return (null, "属性不存在");

        var activeVersionId = await GetActiveModelVersionIdAsync(categoryId);
        if (activeVersionId == null)
        {
            var version = new Core.Entities.CategoryModelVersion
            {
                CategoryId = categoryId,
                VersionNo = "v1",
                Status = "active",
                EffectiveFrom = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow,
                ChangeSummary = "初始版本",
            };
            _db.CategoryModelVersions.Add(version);
            await _db.SaveChangesAsync();
            activeVersionId = version.ModelVersionId;
        }

        var exists = await _db.CategoryAttributes.AnyAsync(ca =>
            ca.CategoryId == categoryId && ca.AttributeId == attributeId && ca.ModelVersionId == activeVersionId);
        if (exists) return (null, "该属性已绑定到此类目");

        var binding = new Core.Entities.CategoryAttribute
        {
            CategoryId = categoryId,
            AttributeId = attributeId,
            ModelVersionId = activeVersionId,
            IsRequired = isRequired,
            IsSaleAxis = isSaleAxis,
            GroupType = groupType,
            GroupName = groupName,
            OrderNum = orderNum,
        };
        _db.CategoryAttributes.Add(binding);
        await _db.SaveChangesAsync();
        return (binding, null);
    }

    /// <summary>解绑类目属性</summary>
    public async Task<string?> UnbindAttributeAsync(long categoryId, long bindId)
    {
        var binding = await _db.CategoryAttributes.FindAsync(bindId);
        if (binding == null) return "绑定记录不存在";
        if (binding.CategoryId != categoryId) return "操作无效";
        _db.CategoryAttributes.Remove(binding);
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>按名称查找类目，不存在则在"1688导入"父节点下自动创建</summary>
    public async Task<long> FindOrCreateByNameAsync(string categoryName)
    {
        var existing = await _db.Categories.FirstOrDefaultAsync(c => c.Name == categoryName);
        if (existing != null) return existing.CategoryId;

        // 找或创建"1688导入"一级父类目
        var parent = await _db.Categories.FirstOrDefaultAsync(c => c.Name == "1688导入");
        if (parent == null)
        {
            parent = new Core.Entities.Category
            {
                Name = "1688导入",
                Level = 1,
                OrderNum = 999,
                Status = 1,
                CreateTime = DateTime.UtcNow,
            };
            _db.Categories.Add(parent);
            await _db.SaveChangesAsync();
        }

        var newCat = new Core.Entities.Category
        {
            ParentId = parent.CategoryId,
            Name = categoryName,
            Level = 2,
            OrderNum = 0,
            Status = 1,
            CreateTime = DateTime.UtcNow,
        };
        _db.Categories.Add(newCat);
        await _db.SaveChangesAsync();
        return newCat.CategoryId;
    }
}
