using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Tests;

public class ProductionOrderTests
{
    [Fact]
    public async Task ExplodeOperations_PreservesQuantityAndPredecessorOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options;
        await using var db = new MesDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var product = new MesProduct { ProductCode = "P-PO-001", ProductName = "测试产品", CreatedAt = DateTime.UtcNow };
        var routing = new MesRouting
        {
            Product = product,
            RoutingCode = "RT-PO-001",
            Operations = new List<MesRoutingOperation>
            {
                new() { Sequence = 10, OperationCode = "CUT", OperationName = "切割", StandardCycleSec = 30 },
                new() { Sequence = 20, OperationCode = "ASSY", OperationName = "装配", StandardCycleSec = 60, PredecessorSequence = 10 },
            },
        };
        db.Routings.Add(routing);
        await db.SaveChangesAsync();

        var service = new ProductionOrderService(db);
        var order = await service.CreateAsync(new MesProductionOrder
        {
            ProductId = product.ProductId,
            RoutingId = routing.RoutingId,
            PlannedQty = 120,
            DueDate = DateTime.Today.AddDays(5),
        }, 1);

        var tasks = await service.ExplodeOperationsAsync(order.ProductionOrderId);

        tasks.Select(x => x.Sequence).Should().Equal(10, 20);
        tasks.Select(x => x.PlannedQty).Should().AllBeEquivalentTo(120);
        tasks[1].PredecessorTaskId.Should().Be(tasks[0].OperationTaskId);
    }

    [Fact]
    public async Task Transition_RejectsCompletedOrderReturningToDraft()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options;
        await using var db = new MesDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var order = new MesProductionOrder
        {
            ProductionOrderCode = "PO-STATE-001",
            PlannedQty = 1,
            Status = "completed",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync();

        var service = new ProductionOrderService(db);
        var act = () => service.TransitionAsync(order.ProductionOrderId, "draft");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*completed*draft*");
    }
}
