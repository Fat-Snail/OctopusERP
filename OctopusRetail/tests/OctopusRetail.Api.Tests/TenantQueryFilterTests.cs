using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Persistence;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// M1 — fail-closed 全局过滤器的直接验证（不经 HTTP）。
///
/// 这里刻意用**多个独立 DbContext 实例**查询同一个库，用来证明
/// EF Core 的模型缓存没有把首个上下文的租户固化下来 ——
/// 这是多租户全局过滤器最经典的错误实现方式（捕获 Scoped 服务导致跨请求泄漏）。
/// </summary>
public class TenantQueryFilterTests
{
    /// <summary>可变租户上下文，模拟中间件解析结果</summary>
    private sealed class MutableTenantContext : ITenantContext
    {
        public string? TenantId { get; private set; }
        public bool IsResolved => TenantId is not null;
        public IReadOnlyList<string> AvailableTenants { get; private set; } = Array.Empty<string>();
        public void SetAvailableTenants(IReadOnlyList<string> tenants) => AvailableTenants = tenants;
        public void Resolve(string tenantId) => TenantId = tenantId;
    }

    private static (SqliteConnection conn, DbContextOptions<RetailDbContext> options) NewStore()
    {
        var conn = new SqliteConnection($"Data Source=qf_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        conn.Open();
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseSqlite(conn).Options;
        return (conn, options);
    }

    private static RetailDbContext NewContext(
        DbContextOptions<RetailDbContext> options, string? tenant)
    {
        var ctx = new MutableTenantContext();
        if (tenant is not null) ctx.Resolve(tenant);
        return new RetailDbContext(options, ctx);
    }

    [Fact]
    public void TenantScopedEntity_WithoutFilter_GuardThrows()
    {
        var options = new DbContextOptionsBuilder<UnfilteredContext>()
            .UseSqlite("Data Source=:memory:").Options;
        using var ctx = new UnfilteredContext(options);

        // 访问 Model 触发 OnModelCreating → 护栏应立刻失败
        var act = () => _ = ctx.Model;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ITenantScoped*全局过滤器*");
    }

    [Fact]
    public async Task MemberProfiles_WhenTenantUnresolved_ReturnsEmptySet()
    {
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var seed = NewContext(options, null))
        {
            seed.Database.EnsureCreated();
            seed.MemberProfiles.AddRange(
                new RetailMemberProfile { TenantId = "t_a", KcSub = "s1", DisplayName = "A1", CreatedAt = DateTime.UtcNow },
                new RetailMemberProfile { TenantId = "t_b", KcSub = "s2", DisplayName = "B1", CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        using var unresolved = NewContext(options, null);
        var count = await unresolved.MemberProfiles.CountAsync();

        count.Should().Be(0,
            "租户未解析时必须匹配空集（fail-closed），绝不能回落到返回全部数据");
    }

    [Fact]
    public async Task MemberProfiles_WithDifferentTenantContexts_AreIsolated()
    {
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var seed = NewContext(options, null))
        {
            seed.Database.EnsureCreated();
            seed.MemberProfiles.AddRange(
                new RetailMemberProfile { TenantId = "t_a", KcSub = "s1", DisplayName = "A1", CreatedAt = DateTime.UtcNow },
                new RetailMemberProfile { TenantId = "t_a", KcSub = "s2", DisplayName = "A2", CreatedAt = DateTime.UtcNow },
                new RetailMemberProfile { TenantId = "t_b", KcSub = "s3", DisplayName = "B1", CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        // 三个独立上下文，共用同一份 EF 模型缓存
        using (var a = NewContext(options, "t_a"))
            (await a.MemberProfiles.Select(p => p.DisplayName).ToListAsync())
                .Should().BeEquivalentTo(new[] { "A1", "A2" });

        using (var b = NewContext(options, "t_b"))
            (await b.MemberProfiles.Select(p => p.DisplayName).ToListAsync())
                .Should().BeEquivalentTo(new[] { "B1" },
                    "模型是缓存共享的，但过滤器必须使用当前上下文实例的租户");

        // 回到 t_a，确认没有被 t_b 污染
        using (var a2 = NewContext(options, "t_a"))
            (await a2.MemberProfiles.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ControlPlaneTables_AreNotTenantFiltered()
    {
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var seed = NewContext(options, null))
        {
            seed.Database.EnsureCreated();
            RetailDbSeeder.Seed(seed);
        }

        // 租户未解析时，仍必须能列出我的 workspace / 读取租户目录
        using var unresolved = NewContext(options, null);
        (await unresolved.Tenants.CountAsync()).Should().Be(3);
        (await unresolved.Memberships.CountAsync()).Should().Be(4);
    }

    /// <summary>刻意缺少租户过滤器的上下文，用于验证护栏</summary>
    private sealed class UnfilteredContext(DbContextOptions<UnfilteredContext> options)
        : DbContext(options)
    {
        public DbSet<RetailMemberProfile> Profiles => Set<RetailMemberProfile>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<RetailMemberProfile>(e =>
            {
                e.ToTable("unfiltered_profile");
                e.HasKey(x => x.ProfileId);
            });
            // 故意不调用 HasQueryFilter —— 护栏必须抛出
            TenantFilterGuard.EnsureAllTenantScopedEntitiesAreFiltered(b);
        }
    }
}
