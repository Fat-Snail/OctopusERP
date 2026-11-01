using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusMES.Api.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

public class SchedulingPlanApiTests : IClassFixture<MesTestFactory>
{
    private readonly MesTestFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public SchedulingPlanApiTests(MesTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task CreatePlan_ReturnsDisplaySlots_AndDateQueryReturnsOverlap()
    {
        var from = new DateTime(2026, 7, 26, 8, 0, 0, DateTimeKind.Utc);
        var orderId = await SeedSchedulableOrderAsync(from);

        var create = await _client.PostAsJsonAsync("/api/scheduling/plans", new
        {
            productionOrderId = orderId,
            from,
            to = from.AddDays(1),
        });
        var createDoc = await create.Content.ReadFromJsonAsync<JsonDocument>(Json);
        createDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var created = createDoc.RootElement.GetProperty("data");
        created.GetProperty("slots").GetArrayLength().Should().Be(1);
        created.GetProperty("slots")[0].GetProperty("taskName").GetString().Should().Be("动态装配");
        created.GetProperty("slots")[0].GetProperty("resourceName").GetString().Should().Be("动态装配工位");

        var list = await _client.GetFromJsonAsync<JsonDocument>(
            $"/api/scheduling/plans?from={Uri.EscapeDataString(from.AddHours(1).ToString("O"))}&to={Uri.EscapeDataString(from.AddHours(2).ToString("O"))}", Json);
        var rows = list!.RootElement.GetProperty("data");
        rows.EnumerateArray().Should().Contain(x =>
            x.GetProperty("schedulePlanId").GetInt64() == created.GetProperty("schedulePlanId").GetInt64());
    }

    [Fact]
    public async Task Publish_RejectsPlanWithoutSlots()
    {
        long planId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
            var order = new MesProductionOrder
            {
                ProductionOrderCode = $"PO-EMPTY-{Guid.NewGuid():N}",
                PlannedQty = 1,
                Status = "draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            var plan = new MesSchedulePlan
            {
                PlanCode = $"SIM-EMPTY-{Guid.NewGuid():N}",
                ProductionOrderId = order.ProductionOrderId,
                CreatedAt = DateTime.UtcNow,
            };
            order.Tasks = [];
            db.ProductionOrders.Add(order);
            await db.SaveChangesAsync();
            plan.ProductionOrderId = order.ProductionOrderId;
            db.SchedulePlans.Add(plan);
            await db.SaveChangesAsync();
            planId = plan.SchedulePlanId;
        }

        var response = await _client.PostAsJsonAsync($"/api/scheduling/plans/{planId}/publish", new { });
        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(Json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("没有可发布的排程时段");
    }

    [Fact]
    public async Task PublishedAndFrozenState_SurvivesDetailReload()
    {
        var from = new DateTime(2026, 7, 27, 8, 0, 0, DateTimeKind.Utc);
        var orderId = await SeedSchedulableOrderAsync(from);
        var createdResponse = await _client.PostAsJsonAsync("/api/scheduling/plans", new
        {
            productionOrderId = orderId,
            from,
            to = from.AddDays(1),
        });
        var createdDoc = await createdResponse.Content.ReadFromJsonAsync<JsonDocument>(Json);
        var planId = createdDoc!.RootElement.GetProperty("data").GetProperty("schedulePlanId").GetInt64();

        await _client.PostAsJsonAsync($"/api/scheduling/plans/{planId}/publish", new { });
        await _client.PostAsJsonAsync(
            $"/api/scheduling/plans/{planId}/freeze?until={Uri.EscapeDataString(from.AddDays(2).ToString("O"))}", new { });

        var detail = await _client.GetFromJsonAsync<JsonDocument>($"/api/scheduling/plans/{planId}", Json);
        var data = detail!.RootElement.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("frozen");
        data.GetProperty("slots").GetArrayLength().Should().Be(1);
        data.GetProperty("slots")[0].GetProperty("taskCode").GetString().Should().Be("OP-DYNAMIC");
    }

    private async Task<long> SeedSchedulableOrderAsync(DateTime from)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var order = new MesProductionOrder
        {
            ProductionOrderCode = $"PO-DYNAMIC-{suffix}",
            PlannedQty = 10,
            Status = "draft",
            CreatedAt = from,
            UpdatedAt = from,
        };
        var task = new MesOperationTask
        {
            ProductionOrder = order,
            Sequence = 10,
            OperationCode = "OP-DYNAMIC",
            OperationName = "动态装配",
            RequiredCapability = $"cap-{suffix}",
            PlannedQty = 10,
            StandardCycleSec = 600,
        };
        var resource = new MesResource
        {
            ResourceCode = $"RES-{suffix}",
            ResourceName = "动态装配工位",
            ResourceKind = "equipment",
            CapabilityCodes = $"cap-{suffix}",
            Status = "available",
        };
        var shift = new MesShift
        {
            ShiftCode = $"SHIFT-{suffix}",
            ShiftName = "动态白班",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(17),
        };
        db.OperationTasks.Add(task);
        db.Resources.Add(resource);
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();
        db.ResourceCalendars.Add(new MesResourceCalendar
        {
            ResourceId = resource.ResourceId,
            WorkDate = from.Date,
            ShiftId = shift.ShiftId,
            IsAvailable = true,
        });
        await db.SaveChangesAsync();
        return order.ProductionOrderId;
    }
}
