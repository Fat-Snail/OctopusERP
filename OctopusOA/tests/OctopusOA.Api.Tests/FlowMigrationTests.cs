using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 验证数据迁移脚本能在「未跑 Seeder」的空库上种出流程定义与示例审批数据，
/// 且重复应用不会产生重复数据。
/// </summary>
public class FlowMigrationTests
{
    private static (OaDbContext db, SqliteConnection conn) FreshDb()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<OaDbContext>().UseSqlite(conn).Options;
        var db = new OaDbContext(options);
        db.Database.EnsureCreated();
        return (db, conn);
    }

    [Fact]
    public void Migration_SeedsFlowDefinitionsAndExamples_OnFreshDb()
    {
        var (db, conn) = FreshDb();
        using var _ = conn;
        using var __ = db;

        DataMigrationRunner.Apply(db, throwOnError: true);

        db.FlowDefinitions.Select(d => d.Code).ToList()
            .Should().Contain(new[] { "leave", "expense", "attendance_fix", "purchase_order" });
        db.FlowInstances.Count(i => i.Title.Contains("示例")).Should().Be(6);
        db.FlowTasks.Count(t => t.Status == "pending").Should().BeGreaterThanOrEqualTo(4);
        db.FlowHistories.Count().Should().BeGreaterThan(0);
        db.FlowExecutions.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Migration_IsIdempotent_WhenAppliedTwice()
    {
        var (db, conn) = FreshDb();
        using var _ = conn;
        using var __ = db;

        DataMigrationRunner.Apply(db, throwOnError: true);
        DataMigrationRunner.Apply(db, throwOnError: true);

        db.FlowInstances.Count(i => i.Title.Contains("示例")).Should().Be(6);
        db.FlowDefinitions.Count().Should().Be(4);
    }
}
