using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;

namespace OctopusCRM.Api.Services;

/// <summary>
/// P16 门户身份绑定：把 JWT 的 sub（UMC UserId）解析为门户客户档案。
///
/// 约定：
/// - 若当前 sub 绑定了某个 CrmCustomer.UmcUserId → 视为"门户客户"，只能访问本人数据；
/// - 若未绑定（如内部员工 admin/user）→ 视为内部用户，保持原有全量可见行为，
///   避免给内部 CRM 操作引入回归。
///
/// 这是"仅本人"行级过滤的唯一权威来源，控制器不要自行解析。
/// </summary>
public class PortalIdentityService(CrmDbContext db)
{
    /// <summary>当前 sub 对应的绑定客户 Id；未绑定返回 null。</summary>
    public async Task<long?> GetCustomerIdAsync(long umcUserId)
    {
        if (umcUserId <= 0) return null;
        return await db.Customers
            .Where(c => c.UmcUserId == umcUserId)
            .Select(c => (long?)c.CustomerId)
            .FirstOrDefaultAsync();
    }

    /// <summary>当前用户是否已绑定为客户门户身份。</summary>
    public async Task<bool> IsPortalCustomerAsync(long umcUserId) =>
        (await GetCustomerIdAsync(umcUserId)).HasValue;
}
