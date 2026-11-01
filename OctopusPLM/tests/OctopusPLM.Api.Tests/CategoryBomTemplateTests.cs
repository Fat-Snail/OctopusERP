using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusPLM.Api.Services;
using OctopusPLM.Core.Entities;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Tests;

public sealed class CategoryBomTemplateTests
{
    [Fact]
    public async Task InitializesOnlyActiveLeavesIdempotentlyAndPublishesOneCurrentVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new PlmDbContext(new DbContextOptionsBuilder<PlmDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var parent = new Category { Name = "手机", Status = 1 };
        var leafA = new Category { Name = "智能手机", Parent = parent, Status = 1 };
        var leafB = new Category { Name = "功能手机", Parent = parent, Status = 1 };
        var inactive = new Category { Name = "停用类目", Parent = parent, Status = 0 };
        db.Categories.AddRange(parent, leafA, leafB, inactive);
        await db.SaveChangesAsync();
        var service = new CategoryBomTemplateService(db);

        var first = await service.InitializeLeafCategoriesAsync(1, "admin", CancellationToken.None);
        var second = await service.InitializeLeafCategoriesAsync(1, "admin", CancellationToken.None);

        first.CreatedCount.Should().Be(2);
        second.CreatedCount.Should().Be(0);
        db.CategoryBomTemplates.Select(x => x.CategoryId).Should().BeEquivalentTo([leafA.CategoryId, leafB.CategoryId]);

        var material = new Material { MaterialCode = "MAT-01", MaterialName = "屏幕", BaseUnit = "片", MaterialType = "purchased_part", Status = "active" };
        db.Materials.Add(material); await db.SaveChangesAsync();
        var v10 = await db.CategoryBomTemplates.SingleAsync(x => x.CategoryId == leafA.CategoryId);
        await service.UpdateDraftAsync(v10.TemplateId, "手机通用 BOM", [new(10, material.MaterialId, 1, 0.02m)], 1, "admin", CancellationToken.None);
        var saved = await db.CategoryBomTemplates.Include(x => x.Items).SingleAsync(x => x.TemplateId == v10.TemplateId);
        saved.TemplateName.Should().Be("手机通用 BOM");
        saved.Items.Should().ContainSingle(x => x.MaterialId == material.MaterialId && x.DefaultLossRate == 0.02m);
        await service.PublishAsync(v10.TemplateId, 1, "admin", CancellationToken.None);
        Func<Task> editPublished = () => service.UpdateDraftAsync(v10.TemplateId, "不可修改", [new(10, material.MaterialId, 2, 0)], 1, "admin", CancellationToken.None);
        await editPublished.Should().ThrowAsync<MaterialConflictException>();
        var v11 = await service.CreateNewVersionAsync(v10.TemplateId, "V1.1", 1, "admin", CancellationToken.None);
        await service.PublishAsync(v11.TemplateId, 1, "admin", CancellationToken.None);

        (await db.CategoryBomTemplates.SingleAsync(x => x.TemplateId == v10.TemplateId)).Status.Should().Be("historical");
        (await db.CategoryBomTemplates.SingleAsync(x => x.TemplateId == v11.TemplateId)).IsLatest.Should().BeTrue();
        (await db.CategoryBomTemplates.CountAsync(x => x.CategoryId == leafA.CategoryId && x.Status == "current")).Should().Be(1);
    }
}
