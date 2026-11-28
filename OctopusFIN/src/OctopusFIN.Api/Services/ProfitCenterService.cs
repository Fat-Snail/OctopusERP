using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

/// <summary>组织与利润中心管理（多组织/多利润中心核算的基础数据层）</summary>
public class ProfitCenterService(FinDbContext db)
{
    public async Task<(List<FinProfitCenter> rows, int total)> ListAsync(
        long? organizationId, string? status, int pageNum, int pageSize)
    {
        var q = db.ProfitCenters
            .Include(x => x.Organization)
            .AsQueryable();
        if (organizationId.HasValue) q = q.Where(x => x.OrganizationId == organizationId.Value);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var total = await q.CountAsync();
        var rows = await q.OrderBy(x => x.CenterCode)
            .Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows, total);
    }

    public async Task<FinProfitCenter?> GetByIdAsync(long id) =>
        await db.ProfitCenters.Include(x => x.Organization).FirstOrDefaultAsync(x => x.ProfitCenterId == id);

    public async Task<(FinProfitCenter? pc, string? error)> CreateAsync(ProfitCenterInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CenterCode)) return (null, "利润中心编码不能为空");
        var exists = await db.ProfitCenters.AnyAsync(x => x.CenterCode == input.CenterCode);
        if (exists) return (null, $"利润中心编码 {input.CenterCode} 已存在");

        var orgExists = await db.Organizations.AnyAsync(x => x.OrganizationId == input.OrganizationId);
        if (!orgExists) return (null, $"组织 {input.OrganizationId} 不存在");

        var pc = new FinProfitCenter
        {
            CenterCode = input.CenterCode,
            CenterName = input.CenterName ?? input.CenterCode,
            OrganizationId = input.OrganizationId,
            Status = input.Status ?? "active",
            Remark = input.Remark,
            CreatedAt = DateTime.UtcNow,
        };
        db.ProfitCenters.Add(pc);
        await db.SaveChangesAsync();
        return (pc, null);
    }

    public async Task<(FinProfitCenter? pc, string? error)> UpdateAsync(long id, ProfitCenterInput input)
    {
        var pc = await db.ProfitCenters.FirstOrDefaultAsync(x => x.ProfitCenterId == id);
        if (pc == null) return (null, "利润中心不存在");

        var codeConflict = await db.ProfitCenters.AnyAsync(x =>
            x.CenterCode == input.CenterCode && x.ProfitCenterId != id);
        if (codeConflict) return (null, $"利润中心编码 {input.CenterCode} 已存在");

        var orgExists = await db.Organizations.AnyAsync(x => x.OrganizationId == input.OrganizationId);
        if (!orgExists) return (null, $"组织 {input.OrganizationId} 不存在");

        pc.CenterCode = input.CenterCode;
        pc.CenterName = input.CenterName ?? pc.CenterName;
        pc.OrganizationId = input.OrganizationId;
        pc.Status = input.Status ?? pc.Status;
        pc.Remark = input.Remark;
        await db.SaveChangesAsync();
        return (pc, null);
    }

    public async Task<(bool ok, string? error)> DeleteAsync(long id)
    {
        // 已被凭证分录引用的利润中心不可删除，避免历史数据悬空
        var refCount = await db.VoucherEntries.CountAsync(x => x.ProfitCenterId == id);
        if (refCount > 0)
            return (false, $"该利润中心已被 {refCount} 条凭证分录引用，不可删除，建议停用");

        var pc = await db.ProfitCenters.FirstOrDefaultAsync(x => x.ProfitCenterId == id);
        if (pc == null) return (false, "利润中心不存在");
        db.ProfitCenters.Remove(pc);
        await db.SaveChangesAsync();
        return (true, null);
    }

    // ===== 组织 CRUD =====

    public async Task<(List<FinOrganization> rows, int total)> ListOrgsAsync(string? status, int pageNum, int pageSize)
    {
        var q = db.Organizations.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var total = await q.CountAsync();
        var rows = await q.OrderBy(x => x.OrgCode)
            .Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows, total);
    }

    public async Task<(FinOrganization? org, string? error)> CreateOrgAsync(OrgInput input)
    {
        if (string.IsNullOrWhiteSpace(input.OrgCode)) return (null, "组织编码不能为空");
        var exists = await db.Organizations.AnyAsync(x => x.OrgCode == input.OrgCode);
        if (exists) return (null, $"组织编码 {input.OrgCode} 已存在");

        var org = new FinOrganization
        {
            OrgCode = input.OrgCode,
            OrgName = input.OrgName ?? input.OrgCode,
            UmcDeptId = input.UmcDeptId,
            Status = input.Status ?? "active",
            IsPrimary = input.IsPrimary,
            CreatedAt = DateTime.UtcNow,
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        return (org, null);
    }
}

public record ProfitCenterInput(
    string CenterCode,
    string? CenterName,
    long OrganizationId,
    string? Status,
    string? Remark);

public record OrgInput(
    string OrgCode,
    string? OrgName,
    long? UmcDeptId,
    string? Status,
    bool IsPrimary);
