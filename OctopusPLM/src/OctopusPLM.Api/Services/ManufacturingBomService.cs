using Microsoft.EntityFrameworkCore;
using OctopusPLM.Core.Entities;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Services;

public sealed record BomMaterialInput(int LineNo, long? MaterialId, string? MaterialCode, string? MaterialName, decimal Quantity, decimal LossRate);

public sealed class ManufacturingBomService(PlmDbContext db)
{
    public async Task<List<ManufacturingBomItem>> ResolveItemsAsync(IEnumerable<BomMaterialInput> inputs, CancellationToken ct)
    {
        var rows = inputs.ToList(); var ids = rows.Where(x => x.MaterialId.HasValue).Select(x => x.MaterialId!.Value).ToList();
        if (ids.Count != ids.Distinct().Count()) throw new ArgumentException("BOM 不能重复选择同一物料");
        var materials = await db.Materials.Where(x => ids.Contains(x.MaterialId)).ToDictionaryAsync(x => x.MaterialId, ct);
        return rows.Select(x =>
        {
            if (!x.MaterialId.HasValue) return new ManufacturingBomItem { LineNo = x.LineNo, MaterialCode = x.MaterialCode?.Trim() ?? "", MaterialName = x.MaterialName?.Trim() ?? "", Quantity = x.Quantity, LossRate = x.LossRate };
            if (!materials.TryGetValue(x.MaterialId.Value, out var material) || material.Status != "active") throw new ArgumentException($"物料 {x.MaterialId} 不存在或已停用");
            return new ManufacturingBomItem { LineNo = x.LineNo, MaterialId = material.MaterialId, MaterialCode = material.MaterialCode, MaterialName = material.MaterialName, MaterialSpecification = material.Specification, MaterialUnit = material.BaseUnit, Quantity = x.Quantity, LossRate = x.LossRate };
        }).ToList();
    }

    public async Task ApplyTemplateAsync(long versionId, long templateId, long userId, string userName, CancellationToken ct)
    {
        var version = await db.ManufacturingVersions.Include(x => x.Product).Include(x => x.BomItems).SingleOrDefaultAsync(x => x.ManufacturingVersionId == versionId, ct) ?? throw new KeyNotFoundException("制造版本不存在");
        if (version.Status != "draft") throw new MaterialConflictException("已发布制造版本不可套用模板");
        var template = await db.CategoryBomTemplates.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Material).SingleOrDefaultAsync(x => x.TemplateId == templateId, ct) ?? throw new KeyNotFoundException("BOM 模板不存在");
        if (template.CategoryId != version.Product!.CategoryId) throw new ArgumentException("模板与商品类目不匹配");
        if (template.Status != "current") throw new ArgumentException("只能套用当前模板");
        if (template.Items.Any(x => x.Material == null || x.Material.Status != "active")) throw new ArgumentException("模板包含不存在或已停用物料");
        db.ManufacturingBomItems.RemoveRange(version.BomItems);
        version.BomItems = template.Items.Select(x => new ManufacturingBomItem { LineNo = x.LineNo, MaterialId = x.MaterialId, MaterialCode = x.Material!.MaterialCode, MaterialName = x.Material.MaterialName, MaterialSpecification = x.Material.Specification, MaterialUnit = x.Material.BaseUnit, Quantity = x.DefaultQuantity, LossRate = x.DefaultLossRate }).ToList();
        db.MaterialTemplateAudits.Add(new() { Action = "apply", EntityType = "bom_template", EntityId = templateId, OperatorId = userId, OperatorName = userName, ManufacturingVersionId = versionId });
        await db.SaveChangesAsync(ct);
    }

    public async Task<CategoryBomTemplate> ExtractAsync(long versionId, string templateName, string templateVersion, long userId, string userName, CancellationToken ct)
    {
        var version = await db.ManufacturingVersions.AsNoTracking().Include(x => x.Product).Include(x => x.BomItems).SingleOrDefaultAsync(x => x.ManufacturingVersionId == versionId, ct) ?? throw new KeyNotFoundException("制造版本不存在");
        if (version.BomItems.Count == 0 || version.BomItems.Any(x => !x.MaterialId.HasValue)) throw new ArgumentException("BOM 物料尚未完整匹配物料库");
        if (await db.CategoryBomTemplates.AnyAsync(x => x.CategoryId == version.Product!.CategoryId && x.Version == templateVersion, ct)) throw new MaterialConflictException("该类目模板版本已存在");
        var template = new CategoryBomTemplate { CategoryId = version.Product!.CategoryId, TemplateName = templateName.Trim(), Version = templateVersion.Trim(), Status = "draft", SourceManufacturingVersionId = versionId, CreatedBy = userId, Items = version.BomItems.Select(x => new CategoryBomTemplateItem { LineNo = x.LineNo, MaterialId = x.MaterialId!.Value, DefaultQuantity = x.Quantity, DefaultLossRate = x.LossRate }).ToList() };
        db.CategoryBomTemplates.Add(template); await db.SaveChangesAsync(ct);
        db.MaterialTemplateAudits.Add(new() { Action = "extract", EntityType = "bom_template", EntityId = template.TemplateId, OperatorId = userId, OperatorName = userName, ManufacturingVersionId = versionId }); await db.SaveChangesAsync(ct); return template;
    }
}
