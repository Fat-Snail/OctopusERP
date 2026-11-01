using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Tests;

public sealed class PlmSyncManagementApiTests : IClassFixture<MesTestFactory>
{
    private readonly MesTestFactory _factory;
    private readonly HttpClient _client;

    public PlmSyncManagementApiTests(MesTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SyncRecords_UsesServerFiltersAndClampsPageSize()
    {
        var id = await SeedAsync();

        var response = await _client.GetFromJsonAsync<JsonDocument>("/api/mes-products/sync-records?status=manual_action&keyword=PHONE&page=1&pageSize=500");

        var data = response!.RootElement.GetProperty("data");
        data.GetProperty("pageSize").GetInt32().Should().Be(50);
        data.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        data.GetProperty("rows")[0].GetProperty("syncRecordId").GetInt64().Should().Be(id);
    }

    [Fact]
    public async Task SyncRecordDetail_ReturnsAttemptsSnapshotAndRelatedOrders()
    {
        var id = await SeedAsync();

        var response = await _client.GetFromJsonAsync<JsonDocument>($"/api/mes-products/sync-records/{id}");

        var data = response!.RootElement.GetProperty("data");
        data.GetProperty("attempts").GetArrayLength().Should().BeGreaterThan(0);
        data.GetProperty("snapshot").GetProperty("operations").GetArrayLength().Should().Be(1);
        data.GetProperty("relatedOrders").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Retry_RejectsCommonUser()
    {
        var id = await SeedAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/mes-products/sync-records/{id}/retry");
        request.Headers.Add("X-Test-UserId", "2");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    private async Task<long> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var snapshot = new MesManufacturingSnapshot
        {
            PlmProductId = Random.Shared.NextInt64(10000, 99999), PlmManufacturingVersionId = Random.Shared.NextInt64(10000, 99999),
            ProductCode = $"PHONE-{suffix}", ProductName = "测试手机", ProductVersion = "V1", BomVersion = "B1", RoutingVersion = "R1",
            PayloadHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), Status = "active", SyncedAt = DateTime.UtcNow,
            Operations = [new MesSnapshotOperation { Sequence = 10, OperationCode = "ASSY", OperationName = "装配", RequiredCapability = "PHONE_ASSY", StandardCycleSec = 60 }],
        };
        db.ManufacturingSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
        var record = new MesPlmProductSyncRecord
        {
            PlmProductId = snapshot.PlmProductId, PlmManufacturingVersionId = snapshot.PlmManufacturingVersionId, PlmVersion = "V1",
            ManufacturingSnapshotId = snapshot.ManufacturingSnapshotId, Status = "manual_action", ErrorMessage = "资源能力缺失",
            RequiresManualAction = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            Attempts = [new MesPlmSyncAttempt { AttemptNumber = 1, Status = "failed", ErrorCode = "PAYLOAD_INVALID", ErrorMessage = "资源能力缺失", StartedAt = DateTime.UtcNow, FinishedAt = DateTime.UtcNow }],
        };
        db.PlmProductSyncRecords.Add(record);
        db.ProductionOrders.Add(new MesProductionOrder { ProductionOrderCode = $"PO-{suffix}", ManufacturingSnapshotId = snapshot.ManufacturingSnapshotId, PlannedQty = 1, CreatedBy = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return record.SyncRecordId;
    }
}
