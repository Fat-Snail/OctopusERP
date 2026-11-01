using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace OctopusUMC.Infrastructure.Persistence;

/// <summary>
/// UMC 数据迁移执行器。
///
/// 为什么需要它：UMC 用的是 `EnsureCreated()` + 幂等 Seeder，**没有 EF Migrations**。
/// Seeder 只在空库时插入，所以「已经有数据的库」里残留的旧内容（比如早期直接写进去的
/// 真实员工姓名）永远不会被修正。这类**存量数据订正**必须有单独的、只执行一次的机制。
///
/// 约定：
/// - 迁移脚本放在 `Persistence/Migrations/*.sql`，以 **EmbeddedResource** 编译进程序集，
///   避免运行目录少文件导致升级静默失效。
/// - 文件名即迁移 Id（按名称排序执行），执行后写入 `sys_data_migration` 表。
/// - 脚本必须**幂等**：已经迁移过的库再次执行也不能出错（用于手工重放）。
///
/// 生产切 MySQL 后，schema 变更请改走 EF Migrations；本机制只适用于数据订正。
/// </summary>
public static class DataMigrationRunner
{
    private const string ResourcePrefix = "OctopusUMC.Infrastructure.Persistence.Migrations.";
    private const string Table = "sys_data_migration";

    /// <summary>在 EnsureCreated + Seeder 之后调用</summary>
    public static void Apply(ApplicationDbContext db, Action<string>? log = null)
    {
        try
        {
            EnsureTable(db);

            var applied = LoadApplied(db);
            var migrations = LoadEmbeddedMigrations();

            foreach (var (id, sql) in migrations)
            {
                if (applied.Contains(id))
                {
                    log?.Invoke($"UMC 迁移已应用，跳过 {id}");
                    continue;
                }

                log?.Invoke($"UMC 开始应用迁移 {id}");
                ExecSql(db, sql);
                MarkApplied(db, id);
                log?.Invoke($"UMC 迁移完成 {id}");
            }
        }
        catch (Exception ex)
        {
            // 迁移失败不能阻塞启动；但要显式告警，避免像 ADR-007 自愈那样被静默吞掉
            log?.Invoke($"UMC 数据迁移失败（服务继续启动，请人工检查 sys_data_migration）: {ex.Message}");
        }
    }

    private static void EnsureTable(ApplicationDbContext db) =>
        ExecSql(db, $"""
            CREATE TABLE IF NOT EXISTS "{Table}" (
                "Id"        TEXT NOT NULL PRIMARY KEY,
                "AppliedAt" TEXT NOT NULL,
                "Note"      TEXT NULL
            );
            """);

    /// <summary>
    /// 用原始 ADO 执行 SQL。
    ///
    /// 不用 `ExecuteSqlRaw`：它会把 SQL **无条件**交给 `string.Format` 处理，
    /// 于是脚本注释里出现 `{Foo}` 这类花括号就会被当成格式说明符并抛
    /// `FormatException: Expected an ASCII digit`（实测踩过）。
    /// 走 ADO 还能一次执行多语句，且不依赖 EF 的参数解析。
    /// </summary>
    private static void ExecSql(ApplicationDbContext db, string sql)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        finally
        {
            if (opened) conn.Close();
        }
    }

    private static HashSet<string> LoadApplied(ApplicationDbContext db)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT \"Id\" FROM \"{Table}\"";
            using var r = cmd.ExecuteReader();
            while (r.Read()) set.Add(r.GetString(0));
        }
        finally
        {
            if (opened) conn.Close();
        }
        return set;
    }

    private static void MarkApplied(ApplicationDbContext db, string id) =>
        ExecSql(db, 
            $"INSERT INTO \"{Table}\" (\"Id\", \"AppliedAt\") VALUES ('{id.Replace("'", "''")}', " +
            $"'{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

    private static List<(string Id, string Sql)> LoadEmbeddedMigrations()
    {
        var asm = Assembly.GetExecutingAssembly();
        return asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                        && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => (Id: n[ResourcePrefix.Length..^4], Sql: ReadResource(asm, n)))
            .ToList();
    }

    private static string ReadResource(Assembly asm, string name)
    {
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"找不到迁移资源 {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
