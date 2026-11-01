using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace OctopusUMC.Infrastructure.Persistence;

/// <summary>
/// SQLite 开发库的模型驱动 schema 自愈（ADR-007）。
///
/// 背景：`EnsureCreated()` **只在库文件不存在时建表**。开发机上的
/// `data/octopus_umc.db` 是历史文件，后来新增的表（sys_job_log /
/// sys_user_notification / sys_wechat_login）不会被补建。
///
/// 后果不是"少个功能"，而是**整个服务被拖死**：
/// JobSchedulerService 写 sys_job_log 抛 `no such table`，
/// 后台任务的未处理异常触发 HostOptions.BackgroundServiceExceptionBehavior = StopHost，
/// UMC 直接停服（实测日志：`FTL ... A BackgroundService has thrown an unhandled exception`
/// → `Application is shutting down`），表现为 5173 点什么都没反应。
///
/// OA / PLM / MES 早已实装同类自愈，UMC 一直缺这一步。
/// 生产切 MySQL 后应改走 EF Migrations。
/// </summary>
public static class SchemaUpgrader
{
    public static void EnsureUpToDate(ApplicationDbContext db, Action<string>? log = null)
    {
        if (!db.Database.IsSqlite()) return;

        var conn = db.Database.GetDbConnection();
        var opened = conn.State != ConnectionState.Open;
        if (opened) conn.Open();

        try
        {
            // ⚠️ 顺序很重要：必须先补列，再重放建表/建索引脚本。
            // EF 的 create script 里含 `CREATE INDEX ... ON sys_user (KcSub)`，
            // 如果先建索引而 KcSub 列还没补，SQLite 会报
            // `no such column: "KcSub"` 并让整个自愈过程失败（实测踩过）。
            AddMissingColumns(db, conn, log);
            CreateMissingTables(db, conn, log);
        }
        catch (Exception ex)
        {
            // 自愈失败不能阻塞启动（历史上 MES 就因 Seeder 抛异常而连带其它系统起不来）
            log?.Invoke($"SQLite schema 自愈失败（建议删除 data/octopus_umc.db 后重启）: {ex.Message}");
        }
        finally
        {
            if (opened) conn.Close();
        }
    }

    /// <summary>重放 EF 生成的建表/建索引脚本，并注入 IF NOT EXISTS</summary>
    private static void CreateMissingTables(ApplicationDbContext db, System.Data.Common.DbConnection conn, Action<string>? log)
    {
        var script = db.Database.GenerateCreateScript();
        if (string.IsNullOrWhiteSpace(script)) return;

        // "CREATE TABLE X" → "CREATE TABLE IF NOT EXISTS X"（索引同理）
        var patched = Regex.Replace(script, @"CREATE\s+TABLE\s+(?!IF\s+NOT\s+EXISTS)", "CREATE TABLE IF NOT EXISTS ");
        patched = Regex.Replace(patched, @"CREATE\s+UNIQUE\s+INDEX\s+(?!IF\s+NOT\s+EXISTS)", "CREATE UNIQUE INDEX IF NOT EXISTS ");
        patched = Regex.Replace(patched, @"CREATE\s+INDEX\s+(?!IF\s+NOT\s+EXISTS)", "CREATE INDEX IF NOT EXISTS ");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = patched;
        cmd.ExecuteNonQuery();

        log?.Invoke("SQLite 建表脚本已重放（IF NOT EXISTS）");
    }

    /// <summary>遍历 EF 模型，补齐已有表里缺失的列</summary>
    private static void AddMissingColumns(ApplicationDbContext db, System.Data.Common.DbConnection conn, Action<string>? log)
    {
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (string.IsNullOrEmpty(table)) continue;

            var existing = ReadColumns(conn, table);
            if (existing.Count == 0) continue;   // 表不存在，已在上面建过

            var storeId = StoreObjectIdentifier.Table(table, entityType.GetSchema());
            foreach (var prop in entityType.GetProperties())
            {
                var column = prop.GetColumnName(storeId);
                if (string.IsNullOrEmpty(column) || existing.Contains(column)) continue;
                if (prop.IsPrimaryKey()) continue;   // SQLite 不允许 ADD PRIMARY KEY

                var sqlType = prop.GetColumnType();
                if (string.IsNullOrEmpty(sqlType)) continue;

                // 尽量带上模型里声明的默认值：SQLite 的 ADD COLUMN 会给**已有行**填这个默认值。
                // 不带默认值时新列为 NULL，而 EF 的非空属性读到 NULL 会抛
                // `The data is NULL at ordinal N`（实测：sys_user.UserType 全 NULL 导致列表 500）。
                var defaultLiteral = DefaultLiteral(prop);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlType} NULL"
                                  + (defaultLiteral is null ? "" : $" DEFAULT {defaultLiteral}");
                cmd.ExecuteNonQuery();

                // 兜底：把历史行里的 NULL 回填成默认值（对非空属性必须做）
                if (!prop.IsNullable && defaultLiteral is not null)
                {
                    using var backfill = conn.CreateCommand();
                    backfill.CommandText =
                        $"UPDATE \"{table}\" SET \"{column}\" = {defaultLiteral} WHERE \"{column}\" IS NULL";
                    backfill.ExecuteNonQuery();
                }

                log?.Invoke($"SQLite 自愈：{table} 补列 {column}" + (defaultLiteral is null ? "" : $"（默认 {defaultLiteral}）"));
            }
        }
    }

    /// <summary>把 EF 模型里的默认值转成 SQL 字面量；没有默认值时按 CLR 类型给安全兜底</summary>
    private static string? DefaultLiteral(Microsoft.EntityFrameworkCore.Metadata.IProperty prop)
    {
        var modelDefault = prop.GetDefaultValue();
        if (modelDefault is not null)
            return modelDefault switch
            {
                string sv => "'" + sv.Replace("'", "''") + "'",
                bool bv => bv ? "1" : "0",
                _ => Convert.ToString(modelDefault, System.Globalization.CultureInfo.InvariantCulture),
            };

        if (prop.IsNullable) return null;

        // 非空但没有模型默认值：按 CLR 类型兜底，避免留下 NULL
        var clr = prop.ClrType;
        if (clr == typeof(string)) return "''";
        if (clr == typeof(DateTime)) return "'1970-01-01 00:00:00'";
        if (clr == typeof(Guid)) return "'00000000-0000-0000-0000-000000000000'";
        if (clr.IsValueType) return "0";
        return null;
    }

    private static List<string> ReadColumns(System.Data.Common.DbConnection conn, string table)
    {
        var cols = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var r = cmd.ExecuteReader();
        while (r.Read()) cols.Add(r.GetString(1));
        return cols;
    }
}
