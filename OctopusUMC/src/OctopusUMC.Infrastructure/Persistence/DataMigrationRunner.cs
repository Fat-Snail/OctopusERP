namespace OctopusUMC.Infrastructure.Persistence;

/// <summary>
/// UMC 的数据迁移入口（实现在 <see cref="OctopusCommon.Persistence.DataMigrationRunner"/>）。
/// 保留这一层是为了让调用方与测试只依赖本命名空间。
/// </summary>
public static class DataMigrationRunner
{
    private const string Prefix = "OctopusUMC.Infrastructure.Persistence.Migrations.";

    public static void Apply(ApplicationDbContext db, Action<string>? log = null, bool throwOnError = false) =>
        OctopusCommon.Persistence.DataMigrationRunner.Apply(
            db, typeof(DataMigrationRunner).Assembly, Prefix, "sys_data_migration", log, throwOnError);
}
