using Microsoft.EntityFrameworkCore;

namespace OctopusRetail.Api.Persistence;

/// <summary>
/// 结构性护栏：任何实现 <see cref="ITenantScoped"/> 的实体若缺少全局租户过滤器，立即失败。
///
/// 目的：让「新增业务表忘记加隔离」在构建期/启动期暴露，
/// 而不是在生产环境泄漏了跨租户数据之后才被发现。
/// </summary>
public static class TenantFilterGuard
{
    public static void EnsureAllTenantScopedEntitiesAreFiltered(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.GetDeclaredQueryFilters().Any()) continue;

            throw new InvalidOperationException(
                $"实体 {entityType.ClrType.Name} 实现了 ITenantScoped 但未配置租户全局过滤器。" +
                "请在 OnModelCreating 中为其添加 HasQueryFilter，否则会造成跨租户数据泄漏。");
        }
    }
}
