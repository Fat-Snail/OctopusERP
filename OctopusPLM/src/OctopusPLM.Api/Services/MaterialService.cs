using Microsoft.EntityFrameworkCore;
using OctopusPLM.Core.Entities;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Services;

public sealed record MaterialWriteRequest(string MaterialCode, string MaterialName, string? Specification, string BaseUnit, string MaterialType, string Status);
public sealed record MaterialReferences(int ManufacturingBomCount, int TemplateItemCount);
public sealed record PagedMaterials(IReadOnlyList<Material> Rows, int Total, int Page, int PageSize);

public sealed class MaterialService(PlmDbContext db)
{
    public async Task<PagedMaterials> SearchAsync(string? keyword, string? type, string? status, bool selectableOnly, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Materials.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(x => x.MaterialCode.Contains(value) || x.MaterialName.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.MaterialType == type);
        if (selectableOnly) query = query.Where(x => x.Status == "active");
        else if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.MaterialCode).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows, total, page, pageSize);
    }

    public async Task<Material> CreateAsync(MaterialWriteRequest request, long userId, string userName, CancellationToken ct)
    {
        var code = NormalizeAndValidate(request);
        if (await db.Materials.AnyAsync(x => x.MaterialCode == code, ct)) throw new MaterialConflictException("物料编码已存在");
        var now = DateTime.UtcNow;
        var material = new Material { MaterialCode = code, MaterialName = request.MaterialName.Trim(), Specification = request.Specification?.Trim(), BaseUnit = request.BaseUnit.Trim(), MaterialType = request.MaterialType, Status = request.Status, CreatedBy = userId, CreatedAt = now, UpdatedAt = now };
        db.Materials.Add(material); await db.SaveChangesAsync(ct);
        AddAudit("create", material.MaterialId, userId, userName, null); await db.SaveChangesAsync(ct);
        return material;
    }

    public async Task<Material> UpdateAsync(long id, MaterialWriteRequest request, long userId, string userName, CancellationToken ct)
    {
        var code = NormalizeAndValidate(request);
        var material = await db.Materials.SingleOrDefaultAsync(x => x.MaterialId == id, ct) ?? throw new KeyNotFoundException("物料不存在");
        var refs = await GetReferencesAsync(id, ct);
        if (refs.ManufacturingBomCount + refs.TemplateItemCount > 0 && material.MaterialCode != code) throw new MaterialConflictException("已被引用的物料不可修改编码");
        if (await db.Materials.AnyAsync(x => x.MaterialId != id && x.MaterialCode == code, ct)) throw new MaterialConflictException("物料编码已存在");
        material.MaterialCode = code; material.MaterialName = request.MaterialName.Trim(); material.Specification = request.Specification?.Trim(); material.BaseUnit = request.BaseUnit.Trim(); material.MaterialType = request.MaterialType; material.Status = request.Status; material.UpdatedAt = DateTime.UtcNow;
        AddAudit("update", id, userId, userName, null); await db.SaveChangesAsync(ct); return material;
    }

    public async Task<MaterialReferences> GetReferencesAsync(long id, CancellationToken ct) => new(
        await db.ManufacturingBomItems.CountAsync(x => x.MaterialId == id, ct),
        await db.CategoryBomTemplateItems.CountAsync(x => x.MaterialId == id, ct));

    public async Task DeleteAsync(long id, long userId, string userName, CancellationToken ct)
    {
        var material = await db.Materials.SingleOrDefaultAsync(x => x.MaterialId == id, ct) ?? throw new KeyNotFoundException("物料不存在");
        var refs = await GetReferencesAsync(id, ct);
        if (refs.ManufacturingBomCount + refs.TemplateItemCount > 0) throw new MaterialConflictException($"物料已被引用：制造 BOM {refs.ManufacturingBomCount} 处，模板 {refs.TemplateItemCount} 处；请停用物料");
        db.Materials.Remove(material); AddAudit("delete", id, userId, userName, null); await db.SaveChangesAsync(ct);
    }

    private static string NormalizeAndValidate(MaterialWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MaterialCode)) throw new ArgumentException("物料编码不能为空");
        if (string.IsNullOrWhiteSpace(request.MaterialName)) throw new ArgumentException("物料名称不能为空");
        if (string.IsNullOrWhiteSpace(request.BaseUnit)) throw new ArgumentException("基本单位不能为空");
        if (request.Status is not ("active" or "inactive")) throw new ArgumentException("物料状态无效");
        return request.MaterialCode.Trim().ToUpperInvariant();
    }

    private void AddAudit(string action, long id, long userId, string userName, string? detail) => db.MaterialTemplateAudits.Add(new() { Action = action, EntityType = "material", EntityId = id, OperatorId = userId, OperatorName = userName, DetailJson = detail, CreatedAt = DateTime.UtcNow });
}

public sealed class MaterialConflictException(string message) : InvalidOperationException(message);
