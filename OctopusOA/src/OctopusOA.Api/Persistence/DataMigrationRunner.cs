namespace OctopusOA.Api.Persistence;

/// <summary>
/// OA 的数据迁移入口（实现在 <see cref="OctopusCommon.Persistence.DataMigrationRunner"/>）。
/// 用于空库 Seeder 覆盖不到的存量/示例数据订正，脚本位于 Persistence/Migrations/*.sql。
/// </summary>
public static class DataMigrationRunner
{
    private const string Prefix = "OctopusOA.Api.Persistence.Migrations.";

    public static void Apply(OaDbContext db, Action<string>? log = null, bool throwOnError = false) =>
        OctopusCommon.Persistence.DataMigrationRunner.Apply(
            db, typeof(DataMigrationRunner).Assembly, Prefix, "oa_data_migration", log, throwOnError);
}
