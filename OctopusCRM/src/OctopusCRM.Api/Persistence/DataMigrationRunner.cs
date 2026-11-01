namespace OctopusCRM.Api.Persistence;

/// <summary>
/// CRM 的数据迁移入口（实现在 <see cref="OctopusCommon.Persistence.DataMigrationRunner"/>）。
/// </summary>
public static class DataMigrationRunner
{
    private const string Prefix = "OctopusCRM.Api.Persistence.Migrations.";

    public static void Apply(CrmDbContext db, Action<string>? log = null, bool throwOnError = false) =>
        OctopusCommon.Persistence.DataMigrationRunner.Apply(
            db, typeof(DataMigrationRunner).Assembly, Prefix, "crm_data_migration", log, throwOnError);
}
