using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Tests;

public class MesSchemaCompatibilityTests
{
    [Fact]
    public async Task EnsureCompatibleSchema_AddsPlmSyncRecordIdToLegacyProductionOrderTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE mes_production_order (
                    ProductionOrderId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductionOrderCode TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<MesDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new MesDbContext(options);

        MesDbSeeder.EnsureCompatibleSchema(db);

        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT COUNT(*) FROM pragma_table_info('mes_production_order') WHERE name = 'PlmSyncRecordId'";
        Convert.ToInt32(await verify.ExecuteScalarAsync()).Should().Be(1);

        verify.CommandText = "SELECT COUNT(*) FROM pragma_table_info('mes_production_order') WHERE name = 'ManufacturingSnapshotId'";
        Convert.ToInt32(await verify.ExecuteScalarAsync()).Should().Be(1);
        verify.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='mes_manufacturing_snapshot'";
        Convert.ToInt32(await verify.ExecuteScalarAsync()).Should().Be(1);
        verify.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='mes_plm_sync_attempt'";
        Convert.ToInt32(await verify.ExecuteScalarAsync()).Should().Be(1);
    }
}
