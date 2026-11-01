using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusCRM.Api.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusCRM.Api.Tests;

/// <summary>P10：CRM POST /api/contract/sales-qty 商品维度销量查询</summary>
public class SalesQtyTests : IClassFixture<CrmTestFactory>
{
    private readonly HttpClient _client;
    private readonly CrmTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public SalesQtyTests(CrmTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task SalesQty_AggregatesContractItemsByProduct()
    {
        // 注入测试用合同 + 明细（避免依赖种子）
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var customer = await db.Customers.FirstAsync();
            var quote = await db.Quotes.FirstAsync();
            var now = DateTime.UtcNow;

            var c = new CrmContract
            {
                ContractCode = $"TEST-{Guid.NewGuid():N}".Substring(0, 16),
                QuoteId = quote.QuoteId,
                CustomerId = customer.CustomerId,
                Title = "P10 销量测试合同",
                TotalAmount = 100m,
                Status = "active",
                CreatedBy = 1,
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5),
            };
            c.Items.Add(new CrmContractItem { PlmProductId = 555, ProductName = "测试品 A", Quantity = 8m, Unit = "台", UnitPrice = 1m, Amount = 8m });
            c.Items.Add(new CrmContractItem { PlmProductId = 555, ProductName = "测试品 A", Quantity = 5m, Unit = "台", UnitPrice = 1m, Amount = 5m });
            c.Items.Add(new CrmContractItem { PlmProductId = 666, ProductName = "测试品 B", Quantity = 3m, Unit = "台", UnitPrice = 1m, Amount = 3m });
            db.Contracts.Add(c);
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/contract/sales-qty",
            new { productIds = new[] { 555L, 666L }, days = 30 });
        resp.IsSuccessStatusCode.Should().BeTrue();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        var p555 = rows.First(r => r.GetProperty("plmProductId").GetInt64() == 555);
        p555.GetProperty("salesQty").GetDecimal().Should().Be(13m); // 8 + 5
        p555.GetProperty("contractCount").GetInt32().Should().Be(1);
        p555.GetProperty("days").GetInt32().Should().Be(30);

        var p666 = rows.First(r => r.GetProperty("plmProductId").GetInt64() == 666);
        p666.GetProperty("salesQty").GetDecimal().Should().Be(3m);
    }

    [Fact]
    public async Task SalesQty_DraftAndCancelledContractsAreExcluded()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var customer = await db.Customers.FirstAsync();
            var quote = await db.Quotes.FirstAsync();
            var now = DateTime.UtcNow;

            var draft = new CrmContract
            {
                ContractCode = $"DRAFT-{Guid.NewGuid():N}".Substring(0, 16),
                QuoteId = quote.QuoteId, CustomerId = customer.CustomerId,
                Title = "草稿合同", TotalAmount = 0m, Status = "draft",
                CreatedBy = 1, CreatedAt = now, UpdatedAt = now,
            };
            draft.Items.Add(new CrmContractItem { PlmProductId = 777, ProductName = "草稿测试", Quantity = 100m, Unit = "台", UnitPrice = 1m, Amount = 100m });
            db.Contracts.Add(draft);
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/contract/sales-qty",
            new { productIds = new[] { 777L } });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").EnumerateArray().ToList();
        // 草稿合同应被排除
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task SalesQty_EmptyProductIds_ReturnsEmpty()
    {
        var resp = await _client.PostAsJsonAsync("/api/contract/sales-qty",
            new { productIds = Array.Empty<long>() });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
    }
}
