using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusPLM.Api.Services;
using OctopusPLM.Core.Entities;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Tests;

public sealed class ManufacturingBomTemplateApiTests
{
    [Fact]
    public async Task ApplyingCurrentTemplateCreatesIndependentResolvedBomSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new PlmDbContext(new DbContextOptionsBuilder<PlmDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var category = new Category { Name = "手机", Status = 1 };
        var product = new Product { Category = category, ProductName = "测试手机", Status = "active" };
        var material = new Material { MaterialCode = "MAT-SCREEN", MaterialName = "屏幕", Specification = "AMOLED", BaseUnit = "片", MaterialType = "purchased_part", Status = "active" };
        var template = new CategoryBomTemplate { Category = category, Version = "V1.0", Status = "current", IsLatest = true, Items = [new() { LineNo = 10, Material = material, DefaultQuantity = 1, DefaultLossRate = 0.01m }] };
        var version = new ManufacturingVersion { Product = product, Version = "V1", Status = "draft", BomVersion = "B1", RoutingVersion = "R1" };
        db.AddRange(template, version); await db.SaveChangesAsync();
        var service = new ManufacturingBomService(db);

        await service.ApplyTemplateAsync(version.ManufacturingVersionId, template.TemplateId, 1, "admin", CancellationToken.None);
        template.Items[0].DefaultQuantity = 9; await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        var bom = await db.ManufacturingBomItems.SingleAsync();
        bom.MaterialId.Should().Be(material.MaterialId);
        bom.MaterialCode.Should().Be("MAT-SCREEN");
        bom.MaterialName.Should().Be("屏幕");
        bom.MaterialSpecification.Should().Be("AMOLED");
        bom.MaterialUnit.Should().Be("片");
        bom.Quantity.Should().Be(1);
    }
}
