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
