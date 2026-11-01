using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Tests;

/// <summary>
/// MES 真实姓名匿名化。
///
/// 早期 `mes_sync_user` 与 `mes_garment_team` 直接写入了真实员工姓名与拼音账号。
/// 存量库只能靠数据迁移订正——Seeder 只在空库插入，改种子不会影响已有数据。
///
/// 注意：迁移与测试都**只按 UmcUserId 定位**，刻意不在仓库里写任何具体姓名，
/// 否则清理 PII 的脚本本身又成了 PII 泄漏源。
/// </summary>
public class MesAnonymizationTests
{
    private static async Task<(SqliteConnection conn, MesDbContext db)> NewDbAsync()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlite(conn).Options);
        await db.Database.EnsureCreatedAsync();
        return (conn, db);
    }

    /// <summary>构造一个"迁移前"的库：姓名是历史遗留值（这里用占位串，不代表任何真实人员）</summary>
    private static async Task SeedLegacyAsync(MesDbContext db)
    {
        db.SyncUsers.AddRange(
            new MesSyncUser { UmcUserId = 100, UserName = "legacy_user_a", NickName = "历史姓名A", Email = "legacy_a@corp.local", Status = 1, LastSyncAt = DateTime.UtcNow },
            new MesSyncUser { UmcUserId = 104, UserName = "legacy_user_d", NickName = "历史姓名D", Email = "legacy_d@corp.local", Status = 1, LastSyncAt = DateTime.UtcNow },
            new MesSyncUser { UmcUserId = 105, UserName = "legacy_user_e", NickName = "历史姓名E", Email = "legacy_e@corp.local", Status = 1, LastSyncAt = DateTime.UtcNow });

        db.GarmentTeams.AddRange(
            new MesGarmentTeam { TeamCode = "A", TeamName = "A组-历史姓名A", GroupLabel = "A组", Leader = "历史姓名A", LeaderUmcUserId = 100, Status = "active", SortOrder = 1, CreatedAt = DateTime.UtcNow },
            new MesGarmentTeam { TeamCode = "L-历史姓名D", TeamName = "L组-历史姓名D", GroupLabel = "L组", Leader = "历史姓名D", LeaderUmcUserId = 104, Status = "active", SortOrder = 2, CreatedAt = DateTime.UtcNow },
            new MesGarmentTeam { TeamCode = "L-历史姓名E", TeamName = "L组-历史姓名E", GroupLabel = "L组", Leader = "历史姓名E", LeaderUmcUserId = 105, Status = "active", SortOrder = 3, CreatedAt = DateTime.UtcNow },
            // 外发组没有 LeaderUmcUserId，迁移不应改动
            new MesGarmentTeam { TeamCode = "L-外协", TeamName = "L组-外协", GroupLabel = "L组", Leader = "外协", Status = "active", SortOrder = 4, CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();
    }

    [Fact]
    public void MigrationScript_IsEmbeddedInApiAssembly()
    {
        var names = typeof(MesDataMigrationRunner).Assembly.GetManifestResourceNames();
        names.Should().Contain(n => n.EndsWith("20261020_001_anonymize_real_users.sql"),
            $"迁移脚本必须嵌入程序集，否则升级会静默失效。实际资源：{string.Join(", ", names)}");
    }

    [Fact]
    public async Task Migration_AnonymizesExistingDatabaseById()
    {
        var (conn, db) = await NewDbAsync();
        await using var _ = conn;
        await using var __ = db;

        await SeedLegacyAsync(db);
        MesDataMigrationRunner.Apply(db, throwOnError: true);

        var users = await db.SyncUsers.AsNoTracking().ToListAsync();
        users.Single(u => u.UmcUserId == 100).UserName.Should().Be("worker100");
        users.Single(u => u.UmcUserId == 100).NickName.Should().Be("员工100");
        users.Single(u => u.UmcUserId == 104).UserName.Should().Be("worker104");
        users.Single(u => u.UmcUserId == 105).UserName.Should().Be("worker105");
        users.Should().NotContain(u => u.NickName.StartsWith("历史姓名"),
            "迁移后不得残留任何历史姓名");

        var teams = await db.GarmentTeams.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync();

        teams.Single(t => t.SortOrder == 1).Leader.Should().Be("员工100");
        teams.Single(t => t.SortOrder == 1).TeamName.Should().Be("A组-员工100");

        // 组代码里的姓名也要换掉（schedules 引用 TeamId，改代码安全）
        teams.Single(t => t.SortOrder == 2).TeamCode.Should().Be("L-104");
        teams.Single(t => t.SortOrder == 2).TeamName.Should().Be("L组-员工104");
        teams.Single(t => t.SortOrder == 3).TeamCode.Should().Be("L-105");

        // 外发组（无 LeaderUmcUserId）不该被误改
        teams.Single(t => t.SortOrder == 4).TeamCode.Should().Be("L-外协");
        teams.Single(t => t.SortOrder == 4).Leader.Should().Be("外协");
    }

    [Fact]
    public async Task Migration_IsRecordedAndIdempotent()
    {
        var (conn, db) = await NewDbAsync();
        await using var _ = conn;
        await using var __ = db;

        await SeedLegacyAsync(db);

        MesDataMigrationRunner.Apply(db, throwOnError: true);
        var first = await db.GarmentTeams.AsNoTracking()
            .OrderBy(t => t.SortOrder).Select(t => t.TeamName).ToListAsync();

        MesDataMigrationRunner.Apply(db, throwOnError: true);
        var second = await db.GarmentTeams.AsNoTracking()
            .OrderBy(t => t.SortOrder).Select(t => t.TeamName).ToListAsync();

        second.Should().Equal(first);

        var applied = await db.Database
            .SqlQueryRaw<string>("SELECT \"Id\" AS \"Value\" FROM \"mes_data_migration\"")
            .ToListAsync();
        applied.Should().Contain("20261020_001_anonymize_real_users",
            "迁移执行后必须留痕，避免重复执行或无从追溯");
    }

    [Fact]
    public async Task FreshSeededDatabase_ContainsOnlySyntheticIdentities()
    {
        var (conn, db) = await NewDbAsync();
        await using var _ = conn;
        await using var __ = db;

        MesDbSeeder.Seed(db);
        MesDataMigrationRunner.Apply(db, throwOnError: true);

        var users = await db.SyncUsers.AsNoTracking().ToListAsync();
        // 组长账号必须是 worker{id} / 员工{id} 形式（前 3 个是 admin/zhangsan/lisi 约定账号）
        var leaders = users.Where(u => u.UmcUserId >= 100).ToList();
        leaders.Should().NotBeEmpty();
        leaders.Should().OnlyContain(u => u.UserName.StartsWith("worker"));
        leaders.Should().OnlyContain(u => u.NickName.StartsWith("员工"));
        leaders.Should().OnlyContain(u => u.Email.EndsWith("@example.invalid"));

        var teams = await db.GarmentTeams.AsNoTracking().ToListAsync();
        teams.Should().NotBeEmpty();
        teams.Where(t => t.LeaderUmcUserId != null)
            .Should().OnlyContain(t => t.Leader.StartsWith("员工"));

        // 组长的 UMC 关联不能被匿名化打断
        teams.Single(t => t.TeamCode == "A").LeaderUmcUserId.Should().Be(100);
    }
}
