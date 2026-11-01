using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Multitenancy;

namespace OctopusRetail.Api.Persistence;

/// <summary>
/// 零售域数据库上下文。
///
/// 多租户隔离采用 **fail-closed** 全局查询过滤器（design §7 第二道防线）：
///   CurrentTenantId 为 null（租户未解析）时过滤器恒为 false → 匹配空集，而不是全集。
///
/// 这是刻意的设计反转：忘记设置租户的默认后果必须是「查不到数据」，
/// 而不是「查到所有租户的数据」。
///
/// 控制面表（retail_tenant / retail_membership）刻意不带过滤器——列「我的 workspace」
/// 必须能跨租户读取，因此这些表的查询一律强制约束 KcSub。
/// 业务表一律实现 ITenantScoped，由本类自动校验过滤器存在性。
/// </summary>
public class RetailDbContext(DbContextOptions<RetailDbContext> options, ITenantContext tenant)
    : DbContext(options)
{
    private readonly ITenantContext _tenant = tenant;

    public DbSet<RetailTenant> Tenants => Set<RetailTenant>();
    public DbSet<RetailMembership> Memberships => Set<RetailMembership>();
    public DbSet<RetailMemberProfile> MemberProfiles => Set<RetailMemberProfile>();
    public DbSet<RetailInvite> Invites => Set<RetailInvite>();

    /// <summary>
    /// 全局过滤器读取的租户。未解析时为 null —— 过滤器因此匹配空集（fail-closed）。
    /// 刻意走属性而非直接捕获 ITenantContext：EF Core 的模型是跨实例缓存的，
    /// 捕获 Scoped 服务会固化首个请求的租户，造成跨租户泄漏。
    /// </summary>
    public string? CurrentTenantId => _tenant.IsResolved ? _tenant.TenantId : null;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<RetailTenant>(e =>
        {
            e.ToTable("retail_tenant");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasMaxLength(64);
            e.Property(x => x.TenantCode).IsRequired().HasMaxLength(64);
            e.Property(x => x.TenantName).IsRequired().HasMaxLength(128);
            e.Property(x => x.TenantType).IsRequired().HasMaxLength(32);
            e.Property(x => x.ParentTenantId).HasMaxLength(64);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.HasIndex(x => x.TenantCode).IsUnique();
            e.HasIndex(x => x.ParentTenantId);
        });

        b.Entity<RetailMembership>(e =>
        {
            e.ToTable("retail_membership");
            e.HasKey(x => x.MembershipId);
            e.Property(x => x.MembershipId).ValueGeneratedOnAdd();
            e.Property(x => x.KcSub).IsRequired().HasMaxLength(64);
            e.Property(x => x.TenantId).IsRequired().HasMaxLength(64);
            e.Property(x => x.RoleInTenant).IsRequired().HasMaxLength(32);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.Property(x => x.KcSyncState).HasMaxLength(32);
            e.Property(x => x.UserName).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(128);
            // 同一人在同一租户只能有一条有效成员关系
            e.HasIndex(x => new { x.KcSub, x.TenantId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
        });

        b.Entity<RetailMemberProfile>(e =>
        {
            e.ToTable("retail_member_profile");
            e.HasKey(x => x.ProfileId);
            e.Property(x => x.ProfileId).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId).IsRequired().HasMaxLength(64);
            e.Property(x => x.KcSub).IsRequired().HasMaxLength(64);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.HasIndex(x => new { x.TenantId, x.KcSub });

            // ★ 业务表的租户隔离：未解析租户 → 恒 false → 空集
            e.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId);
        });

        b.Entity<RetailInvite>(e =>
        {
            e.ToTable("retail_invite");
            e.HasKey(x => x.InviteId);
            e.Property(x => x.InviteId).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.InviteCode).HasMaxLength(64);
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            e.Property(x => x.RoleInTenant).IsRequired().HasMaxLength(32);
            e.Property(x => x.State).IsRequired().HasMaxLength(32);
            e.Property(x => x.InvitedBySub).IsRequired().HasMaxLength(64);
            e.Property(x => x.AcceptedBySub).HasMaxLength(64);
            e.Property(x => x.ShortCodeHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.ShortCodeHash).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.State });

            // ★ 邀请同样带租户隔离：A 店看不到 B 店的邀请。
            // 接受邀请的跨租户查找在 InviteService 中显式 IgnoreQueryFilters()。
            e.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId);
        });

        // 结构性护栏：任何实现 ITenantScoped 的实体若缺少全局过滤器，构建模型即失败
        TenantFilterGuard.EnsureAllTenantScopedEntitiesAreFiltered(b);
    }
}
