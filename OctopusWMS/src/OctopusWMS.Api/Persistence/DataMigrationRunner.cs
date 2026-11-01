namespace OctopusWMS.Api.Persistence;

/// <summary>
/// WMS 的数据迁移入口（实现在 <see cref="OctopusCommon.Persistence.DataMigrationRunner"/>）。
/// </summary>
public static class DataMigrationRunner
{
    private const string Prefix = "OctopusWMS.Api.Persistence.Migrations.";

    public static void Apply(WmsDbContext db, Action<string>? log = null, bool throwOnError = false) =>
        OctopusCommon.Persistence.DataMigrationRunner.Apply(
            db, typeof(DataMigrationRunner).Assembly, Prefix, "wms_data_migration", log, throwOnError);
}
