using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

/// <summary>
/// P16 门户身份绑定：把 JWT 的 sub（UMC UserId）解析为供应商门户档案。
/// 未绑定的用户（内部员工）视为内部身份，保持原有全量可见行为。
/// </summary>
public class PortalIdentityService(MesDbContext db)
{
    /// <summary>当前 sub 绑定的供应商 Id；未绑定返回 null。</summary>
    public async Task<long?> GetSupplierIdAsync(long umcUserId)
    {
        if (umcUserId <= 0) return null;
        return await db.Suppliers
            .Where(s => s.UmcUserId == umcUserId)
            .Select(s => (long?)s.SupplierId)
            .FirstOrDefaultAsync();
    }
}
