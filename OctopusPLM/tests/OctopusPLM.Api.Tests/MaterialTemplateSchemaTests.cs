using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Tests;

public sealed class MaterialTemplateSchemaTests
{
    [Fact]
    public async Task ModelCreatesMaterialTemplateAuditAndBomSnapshotSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new PlmDbContext(new DbContextOptionsBuilder<PlmDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        foreach (var table in new[] { "plm_material", "plm_category_bom_template", "plm_category_bom_template_item", "plm_material_template_audit" })
        {
            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
            tableCommand.Parameters.AddWithValue("$name", table);
            Convert.ToInt32(await tableCommand.ExecuteScalarAsync()).Should().Be(1, $"{table} should be mapped");
        }

        foreach (var column in new[] { "MaterialId", "MaterialSpecification", "MaterialUnit" })
        {
            await using var columnCommand = connection.CreateCommand();
            columnCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('plm_manufacturing_bom_item') WHERE name=$name";
            columnCommand.Parameters.AddWithValue("$name", column);
            Convert.ToInt32(await columnCommand.ExecuteScalarAsync()).Should().Be(1, $"{column} should be mapped");
        }
    }
}
