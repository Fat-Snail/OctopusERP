using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OctopusRetail.Api.Persistence;

/// <summary>
/// 开发用种子数据。
///
/// 刻意构造「一人多租户」场景，用于验证 workspace 切换与跨租户隔离：
///   kc-zhangsan → 上海南京路店(manager) + 连锁总部(staff)   ← 需要 workspace 切换
///   kc-lisi     → 上海南京路店(staff)
///   kc-wangwu   → 北京王府井店(manager)
///
/// ⚠️ 这些 KcSub 是**占位值**，在真实 Keycloak 里不存在。因此它们的
/// KcSyncState 一律标为 <see cref="MembershipSyncState.Unmanaged"/>，
/// 否则后台对账器会每轮为它们发起 Keycloak 请求并永久失败，刷屏日志
/// 且让「待同步数」失去意义。
///
/// 真实环境要拿到第一个店长权限，请调用 POST /api/retail/tenants 自助建店
/// （见 TenantService），不要依赖本种子。
/// </summary>
public static class RetailDbSeeder
{
    public static void Seed(RetailDbContext db)
    {
        EnsureSchemaUpToDate(db);

        if (!db.Tenants.Any()) SeedDemoData(db);

        // 幂等修补：老库里已经以 pending 落库的占位成员关系要转成 unmanaged，
        // 否则升级后仍会持续重试失败。
        MarkPlaceholderMembershipsUnmanaged(db);
    }

    /// <summary>演示数据用的占位 sub（不对应真实 Keycloak 用户）</summary>
    private static readonly string[] PlaceholderSubs = { "kc-zhangsan", "kc-lisi", "kc-wangwu" };

    private static void MarkPlaceholderMembershipsUnmanaged(RetailDbContext db)
    {
        var stale = db.Memberships
            .IgnoreQueryFilters()
            .Where(m => PlaceholderSubs.Contains(m.KcSub))
            .ToList()
            .Where(m => m.KcSyncState != MembershipSyncState.Unmanaged)
            .ToList();

        if (stale.Count == 0) return;

        foreach (var m in stale)
        {
            m.KcSyncState = MembershipSyncState.Unmanaged;
            m.KcSyncedAt = null;
        }
        db.SaveChanges();
    }

    private static void SeedDemoData(RetailDbContext db)
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        db.Tenants.AddRange(
            new RetailTenant
            {
                TenantId = "t_chain_hq", TenantCode = "HQ", TenantName = "连锁总部",
                TenantType = "chain", Status = "active", CreatedAt = now,
            },
            new RetailTenant
            {
                TenantId = "t_shanghai_001", TenantCode = "SH001", TenantName = "上海南京路店",
                TenantType = "store", ParentTenantId = "t_chain_hq", Status = "active", CreatedAt = now,
            },
            new RetailTenant
            {
                TenantId = "t_beijing_002", TenantCode = "BJ002", TenantName = "北京王府井店",
                TenantType = "store", ParentTenantId = "t_chain_hq", Status = "active", CreatedAt = now,
            });

        db.Memberships.AddRange(
            new RetailMembership
            {
                KcSub = "kc-zhangsan", TenantId = "t_shanghai_001", RoleInTenant = "manager",
                Status = "active", JoinedAt = now.AddDays(-30),
                KcSyncState = MembershipSyncState.Unmanaged,
            },
            new RetailMembership
            {
                KcSub = "kc-zhangsan", TenantId = "t_chain_hq", RoleInTenant = "staff",
                Status = "active", JoinedAt = now.AddDays(-10),
                KcSyncState = MembershipSyncState.Unmanaged,
            },
            new RetailMembership
            {
                KcSub = "kc-lisi", TenantId = "t_shanghai_001", RoleInTenant = "staff",
                Status = "active", JoinedAt = now.AddDays(-20),
                KcSyncState = MembershipSyncState.Unmanaged,
            },
            new RetailMembership
            {
                KcSub = "kc-wangwu", TenantId = "t_beijing_002", RoleInTenant = "manager",
                Status = "active", JoinedAt = now.AddDays(-15),
                KcSyncState = MembershipSyncState.Unmanaged,
            });

        db.MemberProfiles.AddRange(
            new RetailMemberProfile
            {
                TenantId = "t_shanghai_001", KcSub = "kc-zhangsan",
                DisplayName = "张三", Phone = "13800000002", CreatedAt = now,
            },
            new RetailMemberProfile
            {
                TenantId = "t_shanghai_001", KcSub = "kc-lisi",
                DisplayName = "李四", Phone = "13800000003", CreatedAt = now,
            },
            new RetailMemberProfile
            {
                TenantId = "t_beijing_002", KcSub = "kc-wangwu",
                DisplayName = "王五", Phone = "13800000005", CreatedAt = now,
            });

        db.SaveChanges();
    }

    /// <summary>
    /// SQLite 下 `EnsureCreated` 只在库文件不存在时建表，**新增列不会增量同步**。
    /// 这里按需补列（同 ADR-007 的模型驱动自愈思路，零售域只需极简版本）。
    /// 生产切 MySQL 后应改用 EF Migrations。
    /// </summary>
    private static void EnsureSchemaUpToDate(RetailDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != ConnectionState.Open;
        if (opened) conn.Open();
        try
        {
            var cols = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(\"retail_invite\")";
                using var r = cmd.ExecuteReader();
                while (r.Read()) cols.Add(r.GetString(1));
            }

            // 表还不存在：交给 EnsureCreated
            if (cols.Count == 0) return;

            if (!cols.Contains("ShortCodeHash"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText =
                    "ALTER TABLE \"retail_invite\" ADD COLUMN \"ShortCodeHash\" TEXT NULL";
                alter.ExecuteNonQuery();
            }

            // 成员关系的新增列（成员管理页需要展示用户名/姓名）
            var memberCols = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(\"retail_membership\")";
                using var r = cmd.ExecuteReader();
                while (r.Read()) memberCols.Add(r.GetString(1));
            }
            if (memberCols.Count > 0)
            {
                foreach (var col in new[] { "UserName", "DisplayName", "Phone" })
                {
                    if (memberCols.Contains(col)) continue;
                    using var alter = conn.CreateCommand();
                    alter.CommandText =
                        $"ALTER TABLE \"retail_membership\" ADD COLUMN \"{col}\" TEXT NULL";
                    alter.ExecuteNonQuery();
                }
            }
        }
        finally
        {
            if (opened) conn.Close();
        }
    }
}
