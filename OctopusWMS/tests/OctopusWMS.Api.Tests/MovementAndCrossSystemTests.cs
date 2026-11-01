using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusWMS.Api.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusWMS.Api.Tests;

/// <summary>P10/P11 测试：批量库存查询 + 库内移库 + 跨仓调拨 + FEFO 出库</summary>
public class MovementAndCrossSystemTests : IClassFixture<WmsTestFactory>
{
    private readonly HttpClient _client;
    private readonly WmsTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public MovementAndCrossSystemTests(WmsTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // ── P10：跨系统批量库存查询 ─────────────────────────────────────────

    [Fact]
    public async Task ByProducts_ReturnsAggregatedStockAcrossWarehouses()
    {
        // 种子：iPhone 15 Pro (PlmProductId=1) 在仓1=500，仓2=200，合计应是 700
        var resp = await _client.PostAsJsonAsync("/api/inventory/by-products",
            new { productIds = new[] { 1L, 2L } });
        resp.IsSuccessStatusCode.Should().BeTrue();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        var p1 = rows.First(r => r.GetProperty("plmProductId").GetInt64() == 1);
        p1.GetProperty("totalQty").GetDecimal().Should().Be(700m);
        p1.GetProperty("warehouseCount").GetInt32().Should().Be(2);
        p1.GetProperty("availableQty").GetDecimal().Should().Be(600m); // 700 - locked(100)
    }

    [Fact]
    public async Task ByProducts_EmptyList_ReturnsEmpty()
    {
        var resp = await _client.PostAsJsonAsync("/api/inventory/by-products",
            new { productIds = Array.Empty<long>() });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    // ── P11-1/P11-2：货主 + 库区种子数据 ───────────────────────────────

    [Fact]
    public async Task Seeder_CreatesOwnersAndZones()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        (await db.Owners.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
        (await db.Zones.CountAsync(z => z.WarehouseId == 1)).Should().Be(5); // 5 类库区
        (await db.Zones.AnyAsync(z => z.ZoneType == "picking")).Should().BeTrue();
        (await db.Batches.CountAsync()).Should().BeGreaterThanOrEqualTo(3);
    }

    // ── P11-3：FEFO 出库 ────────────────────────────────────────────────

    [Fact]
    public async Task Ship_PrefersEarlierExpiringInventoryWhenFefoApplies()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        // 准备：在仓1 给 PlmProductId=99（独立测试用）放两个批次
        // 批次 A 早过期（2026-09），批次 B 晚过期（2027-12）
        var earlyBatch = new WmsBatch { BatchNo = $"FEFO-A-{Guid.NewGuid():N}", PlmProductId = 99, ProductName = "FEFO测试品", ExpireDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow };
        var lateBatch  = new WmsBatch { BatchNo = $"FEFO-B-{Guid.NewGuid():N}", PlmProductId = 99, ProductName = "FEFO测试品", ExpireDate = new DateTime(2027, 12, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow };
        db.Batches.AddRange(earlyBatch, lateBatch);
        await db.SaveChangesAsync();

        var invEarly = new WmsInventory { WarehouseId = 1, PlmProductId = 99, BatchId = earlyBatch.BatchId, ProductName = "FEFO测试品", Quantity = 50, LockedQuantity = 0, UpdatedAt = DateTime.UtcNow };
        var invLate  = new WmsInventory { WarehouseId = 1, PlmProductId = 99, BatchId = lateBatch.BatchId,  ProductName = "FEFO测试品", Quantity = 50, LockedQuantity = 0, UpdatedAt = DateTime.UtcNow };
        db.Inventories.AddRange(invEarly, invLate);
        await db.SaveChangesAsync();

        // 创建出库单 + 明细 30 件
        var order = new WmsOutboundOrder { OutboundCode = $"OUT-FEFO-{Guid.NewGuid().ToString("N")[..8]}", OutboundType = "sales", WarehouseId = 1, Status = "pending", CreatedBy = 1, CreatedAt = DateTime.UtcNow };
        order.Items.Add(new WmsOutboundItem { PlmProductId = 99, ProductName = "FEFO测试品", RequestedQty = 30, ShippedQty = 0 });
        db.OutboundOrders.Add(order);
        await db.SaveChangesAsync();

        // 触发发货
        var resp = await _client.PutAsync($"/api/outbound/{order.OutboundId}/ship", null);
        resp.IsSuccessStatusCode.Should().BeTrue();

        // 重新查库存：早过期批次应优先消耗 30 件 → 剩余 20，晚过期批次保持 50
        await db.Entry(invEarly).ReloadAsync();
        await db.Entry(invLate).ReloadAsync();
        invEarly.Quantity.Should().Be(20m);
        invLate.Quantity.Should().Be(50m);
    }

    // ── P11-4：库内移库 ────────────────────────────────────────────────

    [Fact]
    public async Task Move_TransfersQuantityBetweenLocationsInSameWarehouse()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        // 取仓1 中 LocationId=1 的库存（种子 InventoryId=1 / Quantity=500 / LockedQuantity=100）
        var src = await db.Inventories.FirstAsync(x => x.InventoryId == 1);
        src.WarehouseId.Should().Be(1);
        var beforeQty = src.Quantity;

        // 移库 50 件到仓1 的 LocationId=2
        var resp = await _client.PostAsJsonAsync("/api/inventory/move",
            new { inventoryId = 1L, toLocationId = 2L, quantity = 50m });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        await db.Entry(src).ReloadAsync();
        src.Quantity.Should().Be(beforeQty - 50m);

        // 目标库位应有新库存条目（同 PlmProductId）
        var dst = await db.Inventories
            .Where(x => x.WarehouseId == 1 && x.LocationId == 2 && x.PlmProductId == src.PlmProductId)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();
        dst.Should().NotBeNull();
        dst!.Quantity.Should().BeGreaterThanOrEqualTo(50m);
    }

    [Fact]
    public async Task Move_Insufficient_Returns400()
    {
        // 库存只有 ~500，请求 9999 应失败
        var resp = await _client.PostAsJsonAsync("/api/inventory/move",
            new { inventoryId = 1L, toLocationId = 2L, quantity = 99999m });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task Move_CrossWarehouse_Rejected()
    {
        // 库位 4 在仓2，但 InventoryId=1 在仓1 — 跨仓应拒绝
        var resp = await _client.PostAsJsonAsync("/api/inventory/move",
            new { inventoryId = 1L, toLocationId = 4L, quantity = 10m });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    // ── P11-4：跨仓调拨 ─────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_FullCycle_ShipThenReceiveUpdatesBothWarehouses()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        // 准备：仓3 给 PlmProductId=200 准备库存 100
        var inv = new WmsInventory { WarehouseId = 3, PlmProductId = 200, ProductName = "调拨测试品", Quantity = 100, LockedQuantity = 0, UpdatedAt = DateTime.UtcNow };
        db.Inventories.Add(inv);
        await db.SaveChangesAsync();

        // 创建调拨：仓3 → 仓1 共 30 件
        var createResp = await _client.PostAsJsonAsync("/api/transfer", new
        {
            fromWarehouseId = 3L, toWarehouseId = 1L,
            plmProductId = 200L, productName = "调拨测试品", quantity = 30m, remark = "P11 测试",
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        createDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var transferId = createDoc.RootElement.GetProperty("data").GetProperty("transferId").GetInt64();

        // 发货
        var shipResp = await _client.PutAsync($"/api/transfer/{transferId}/ship", null);
        var shipDoc = await shipResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        shipDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        await db.Entry(inv).ReloadAsync();
        inv.Quantity.Should().Be(70m); // 100 - 30

        // 收货
        var recvResp = await _client.PutAsync($"/api/transfer/{transferId}/receive", null);
        var recvDoc = await recvResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        recvDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var dst = await db.Inventories.FirstOrDefaultAsync(x => x.WarehouseId == 1 && x.PlmProductId == 200);
        dst.Should().NotBeNull();
        dst!.Quantity.Should().Be(30m);

        var t = await db.Transfers.FirstAsync(x => x.TransferId == transferId);
        t.Status.Should().Be("received");
        t.ShippedAt.Should().NotBeNull();
        t.ReceivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Transfer_SameWarehouse_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/transfer", new
        {
            fromWarehouseId = 1L, toWarehouseId = 1L,
            plmProductId = 1L, productName = "iPhone 15 Pro", quantity = 10m, remark = "",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }
}
