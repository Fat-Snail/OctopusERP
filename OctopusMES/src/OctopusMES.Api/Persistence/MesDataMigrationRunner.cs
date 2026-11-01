namespace OctopusMES.Api.Persistence;

/// <summary>
/// MES 的数据迁移入口（实现在 <see cref="OctopusCommon.Persistence.DataMigrationRunner"/>）。
/// </summary>
public static class MesDataMigrationRunner
{
    private const string Prefix = "OctopusMES.Api.Persistence.Migrations.";

    public static void Apply(MesDbContext db, ILogger? logger = null, bool throwOnError = false) =>
        OctopusCommon.Persistence.DataMigrationRunner.Apply(
            db, typeof(MesDataMigrationRunner).Assembly, Prefix, "mes_data_migration",
            logger is null ? null : msg => logger.LogWarning("{Msg}", msg), throwOnError);
}
