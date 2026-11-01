using Microsoft.EntityFrameworkCore;
using OctopusPLM.Core.Entities;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Services;

public sealed record TemplateInitializationResult(int LeafCount, int CreatedCount, int ExistingCount);
public sealed record TemplateItemWrite(int LineNo, long MaterialId, decimal DefaultQuantity, decimal DefaultLossRate);

public sealed class CategoryBomTemplateService(PlmDbContext db)
{
    public async Task<TemplateInitializationResult> InitializeLeafCategoriesAsync(long userId, string userName, CancellationToken ct)
    {
        var categories = await db.Categories.Where(x => x.Status == 1).ToListAsync(ct);
        var parentIds = categories.Where(x => x.ParentId.HasValue).Select(x => x.ParentId!.Value).ToHashSet();
        var leaves = categories.Where(x => !parentIds.Contains(x.CategoryId)).ToList();
        var existing = await db.CategoryBomTemplates.Select(x => x.CategoryId).Distinct().ToListAsync(ct);
        var existingSet = existing.ToHashSet(); var created = 0;
        foreach (var leaf in leaves.Where(x => !existingSet.Contains(x.CategoryId)))
        {
            var template = new CategoryBomTemplate { CategoryId = leaf.CategoryId, Version = "V1.0", Status = "draft", IsLatest = false, CreatedBy = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.CategoryBomTemplates.Add(template); created++;
        }
        await db.SaveChangesAsync(ct);
        if (created > 0) { db.MaterialTemplateAudits.Add(new() { Action = "initialize", EntityType = "bom_template", EntityId = 0, OperatorId = userId, OperatorName = userName, DetailJson = $"{{\"createdCount\":{created}}}" }); await db.SaveChangesAsync(ct); }
        return new(leaves.Count, created, leaves.Count - created);
    }

    public async Task EnsureLeafTemplateAsync(long categoryId, long userId, string userName, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.CategoryId == categoryId && x.Status == 1, ct);
        if (category is null) return;
        if (category.ParentId.HasValue)
        {
            var obsoleteParentDrafts = await db.CategoryBomTemplates.Include(x => x.Items).Where(x => x.CategoryId == category.ParentId.Value && x.Status == "draft").ToListAsync(ct);
            db.CategoryBomTemplates.RemoveRange(obsoleteParentDrafts.Where(x => x.Items.Count == 0));
        }
        if (await db.Categories.AnyAsync(x => x.ParentId == categoryId && x.Status == 1, ct) || await db.CategoryBomTemplates.AnyAsync(x => x.CategoryId == categoryId, ct)) { await db.SaveChangesAsync(ct); return; }
        db.CategoryBomTemplates.Add(new() { CategoryId = categoryId, Version = "V1.0", Status = "draft", CreatedBy = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<CategoryBomTemplate>> ListAsync(long categoryId, CancellationToken ct) => await db.CategoryBomTemplates.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Material).Where(x => x.CategoryId == categoryId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<CategoryBomTemplate> CreateNewVersionAsync(long sourceId, string version, long userId, string userName, CancellationToken ct)
    {
        var source = await db.CategoryBomTemplates.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.TemplateId == sourceId, ct) ?? throw new KeyNotFoundException("BOM 模板不存在");
        if (string.IsNullOrWhiteSpace(version) || await db.CategoryBomTemplates.AnyAsync(x => x.CategoryId == source.CategoryId && x.Version == version, ct)) throw new MaterialConflictException("该类目模板版本已存在");
        var created = new CategoryBomTemplate { CategoryId = source.CategoryId, TemplateName = source.TemplateName, Version = version.Trim(), Status = "draft", SourceManufacturingVersionId = source.SourceManufacturingVersionId, CreatedBy = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Items = source.Items.Select(x => new CategoryBomTemplateItem { LineNo = x.LineNo, MaterialId = x.MaterialId, DefaultQuantity = x.DefaultQuantity, DefaultLossRate = x.DefaultLossRate }).ToList() };
        db.CategoryBomTemplates.Add(created); await db.SaveChangesAsync(ct); Audit("new_version", created.TemplateId, userId, userName, sourceId); await db.SaveChangesAsync(ct); return created;
    }

    public async Task<CategoryBomTemplate> UpdateDraftAsync(long id, string templateName, IReadOnlyCollection<TemplateItemWrite> items, long userId, string userName, CancellationToken ct)
    {
        var template = await db.CategoryBomTemplates.Include(x => x.Items).SingleOrDefaultAsync(x => x.TemplateId == id, ct) ?? throw new KeyNotFoundException("BOM 模板不存在");
        if (template.Status != "draft") throw new MaterialConflictException("只有草稿模板可以编辑");
        if (string.IsNullOrWhiteSpace(templateName)) throw new ArgumentException("模板名称不能为空");
        if (items.Count == 0) throw new ArgumentException("BOM 模板至少需要一项物料");
        if (items.Any(x => x.LineNo <= 0 || x.MaterialId <= 0 || x.DefaultQuantity <= 0 || x.DefaultLossRate < 0)) throw new ArgumentException("模板物料的行号、用量或损耗率不合法");
        if (items.Select(x => x.LineNo).Distinct().Count() != items.Count) throw new ArgumentException("模板物料行号不能重复");
        if (items.Select(x => x.MaterialId).Distinct().Count() != items.Count) throw new ArgumentException("同一物料不能重复添加");
        var materialIds = items.Select(x => x.MaterialId).Distinct().ToList();
        if (await db.Materials.CountAsync(x => materialIds.Contains(x.MaterialId) && x.Status == "active", ct) != materialIds.Count) throw new ArgumentException("模板包含不存在或已停用物料");

        template.TemplateName = templateName.Trim();
        template.UpdatedAt = DateTime.UtcNow;
        db.CategoryBomTemplateItems.RemoveRange(template.Items);
        template.Items = items.OrderBy(x => x.LineNo).Select(x => new CategoryBomTemplateItem
        {
            LineNo = x.LineNo,
            MaterialId = x.MaterialId,
            DefaultQuantity = x.DefaultQuantity,
            DefaultLossRate = x.DefaultLossRate
        }).ToList();
        Audit("update", id, userId, userName, null);
        await db.SaveChangesAsync(ct);
        return await db.CategoryBomTemplates.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Material).SingleAsync(x => x.TemplateId == id, ct);
    }

    public async Task PublishAsync(long id, long userId, string userName, CancellationToken ct)
    {
        var template = await db.CategoryBomTemplates.Include(x => x.Items).SingleOrDefaultAsync(x => x.TemplateId == id, ct) ?? throw new KeyNotFoundException("BOM 模板不存在");
        if (template.Status != "draft") throw new MaterialConflictException("只有草稿模板可以发布");
        if (template.Items.Count == 0) throw new ArgumentException("BOM 模板至少需要一项物料");
        var ids = template.Items.Select(x => x.MaterialId).Distinct().ToList();
        if (await db.Materials.CountAsync(x => ids.Contains(x.MaterialId) && x.Status == "active", ct) != ids.Count) throw new ArgumentException("模板包含不存在或已停用物料");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var old = await db.CategoryBomTemplates.Where(x => x.CategoryId == template.CategoryId && x.Status == "current").ToListAsync(ct);
        foreach (var item in old) { item.Status = "historical"; item.IsLatest = false; item.UpdatedAt = DateTime.UtcNow; }
        template.Status = "current"; template.IsLatest = true; template.UpdatedAt = DateTime.UtcNow;
        Audit("publish", id, userId, userName, null); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private void Audit(string action, long id, long userId, string userName, long? source) => db.MaterialTemplateAudits.Add(new() { Action = action, EntityType = "bom_template", EntityId = id, OperatorId = userId, OperatorName = userName, SourceId = source, CreatedAt = DateTime.UtcNow });
}
