using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace OctopusCommon.Persistence;

/// <summary>
/// 数据迁移执行器（UMC / MES / CRM / WMS 共用）。
///
/// 为什么需要它：这些子系统都用 `EnsureCreated()` + 幂等 Seeder，**没有 EF Migrations**。
/// Seeder 只在空库插入，所以「已经有数据的库」里残留的旧内容（比如早期写进去的
/// 真实姓名、真实企业名、有效的身份证号）永远不会被修正。这类**存量数据订正**
/// 必须有单独的、只执行一次的机制。
///
/// 约定：
/// - 迁移脚本放在调用方项目的 `Persistence/Migrations/*.sql`，以 **EmbeddedResource**
///   编译进程序集，避免运行目录缺文件导致升级静默失效。
/// - 文件名即迁移 Id（按名称排序执行），执行后写入调用方指定的记录表。
/// - 脚本必须**幂等**：已经迁移过的库再次执行也不能出错（用于手工重放）。
/// - 脚本应**只按主键/自增列定位**，不要把待清除的敏感原值写进 SQL，
///   否则清理工具本身就成了泄漏源。
///
/// 生产切 MySQL 后，schema 变更请改走 EF Migrations；本机制只适用于数据订正。
/// </summary>
public static class DataMigrationRunner
{
    /// <summary>
    /// 应用指定程序集里全部未执行的迁移脚本。
    /// </summary>
    /// <param name="db">目标库上下文（SQLite 或其它 EF 提供程序）</param>
    /// <param name="scriptAssembly">嵌入迁移脚本的程序集</param>
    /// <param name="resourcePrefix">
    /// 嵌入资源前缀，形如 <c>OctopusMES.Api.Persistence.Migrations.</c>（注意结尾的点）。
    /// 资源名去掉该前缀与 <c>.sql</c> 后缀即为迁移 Id。
    /// </param>
    /// <param name="migrationTable">记录已执行迁移的表名，各子系统用各自的表</param>
    /// <param name="log">可选日志回调</param>
    /// <param name="throwOnError">
    /// 测试用开关。默认 false：迁移失败只记日志、不阻塞启动；
    /// 但这也意味着失败会被静默吞掉，所以单元测试应传 true 以暴露真实异常。
    /// </param>
    public static void Apply(
        DbContext db,
        Assembly scriptAssembly,
        string resourcePrefix,
        string migrationTable,
        Action<string>? log = null,
        bool throwOnError = false)
    {
        try
        {
            EnsureTable(db, migrationTable);

            var applied = LoadApplied(db, migrationTable);
            var migrations = LoadEmbeddedMigrations(scriptAssembly, resourcePrefix);

            foreach (var (id, sql) in migrations)
            {
                if (applied.Contains(id))
                {
                    log?.Invoke($"迁移已应用，跳过 {id}");
                    continue;
                }

                log?.Invoke($"开始应用迁移 {id}");
                ExecSql(db, sql);
                MarkApplied(db, migrationTable, id);
                log?.Invoke($"迁移完成 {id}");
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"数据迁移失败（服务继续启动，请人工检查 {migrationTable}）: {ex.Message}");

            // 没有 logger（单元测试/控制台场景）时至少打到 stderr，
            // 否则会像早期的 schema 自愈那样「静默什么都没做」。
            if (log is null) Console.Error.WriteLine($"[DataMigration:{migrationTable}] 失败: {ex}");
            if (throwOnError) throw;
        }
    }

    private static void EnsureTable(DbContext db, string table) =>
        ExecSql(db, $"""
            CREATE TABLE IF NOT EXISTS "{table}" (
                "Id"        TEXT NOT NULL PRIMARY KEY,
                "AppliedAt" TEXT NOT NULL,
                "Note"      TEXT NULL
            );
            """);

    private static HashSet<string> LoadApplied(DbContext db, string table)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != ConnectionState.Open;
        if (opened) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT \"Id\" FROM \"{table}\"";
            using var r = cmd.ExecuteReader();
            while (r.Read()) set.Add(r.GetString(0));
        }
        finally
        {
            if (opened) conn.Close();
        }
        return set;
    }

    private static void MarkApplied(DbContext db, string table, string id) =>
        ExecSql(db,
            $"INSERT INTO \"{table}\" (\"Id\", \"AppliedAt\") VALUES ('{id.Replace("'", "''")}', " +
            $"'{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

    private static List<(string Id, string Sql)> LoadEmbeddedMigrations(Assembly asm, string prefix)
    {
        return asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal)
                        && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => (Id: n[prefix.Length..^4], Sql: ReadResource(asm, n)))
            .ToList();
    }

    private static string ReadResource(Assembly asm, string name)
    {
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"找不到迁移资源 {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 用原始 ADO 执行 SQL。
    ///
    /// 不用 `ExecuteSqlRaw`：它会把 SQL **无条件**交给 `string.Format` 处理，
    /// 于是脚本注释里出现 `{Foo}` 这类花括号就会被当成格式说明符并抛
    /// `FormatException: Expected an ASCII digit`，且异常被 catch 后表现为
    /// 「迁移什么都没做」，极难排查（实测踩过）。
    /// 走 ADO 还能一次执行多语句，且不依赖 EF 的参数解析。
    /// </summary>
    private static void ExecSql(DbContext db, string sql)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != ConnectionState.Open;
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
}
