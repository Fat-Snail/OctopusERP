using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusFIN.Api.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusFIN.Api.Tests;

public class VoucherTests : IClassFixture<FinTestFactory>
{
    private readonly HttpClient _client;
    private readonly FinTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public VoucherTests(FinTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task CreateVoucher_BalancedEntries_Succeeds()
    {
        var resp = await _client.PostAsJsonAsync("/api/voucher", new
        {
            voucherDate = DateTime.UtcNow,
            voucherType = "normal",
            summary = "测试凭证",
            entries = new[]
            {
                new { accountId = 2L, summary = "银行收款", debitAmount = 1000m, creditAmount = 0m, currency = "CNY" },
                new { accountId = 11L, summary = "确认收入", debitAmount = 0m, creditAmount = 1000m, currency = "CNY" },
            }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("totalDebit").GetDecimal().Should().Be(1000m);
        data.GetProperty("totalCredit").GetDecimal().Should().Be(1000m);
        data.GetProperty("status").GetString().Should().Be("draft");
    }

    [Fact]
    public async Task CreateVoucher_Unbalanced_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/voucher", new
        {
            voucherDate = DateTime.UtcNow,
            entries = new[]
            {
                new { accountId = 2L, summary = "x", debitAmount = 100m, creditAmount = 0m, currency = "CNY" },
                new { accountId = 11L, summary = "y", debitAmount = 0m, creditAmount = 50m, currency = "CNY" },
            }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("借贷不平");
    }

    [Fact]
    public async Task CreateVoucher_LessThanTwoEntries_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/voucher", new
        {
            entries = new[]
            {
                new { accountId = 2L, summary = "x", debitAmount = 100m, creditAmount = 0m, currency = "CNY" },
            }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task PostVoucher_DraftToPosted_Succeeds()
    {
        var createResp = await _client.PostAsJsonAsync("/api/voucher", new
        {
            voucherDate = DateTime.UtcNow,
            summary = "过账测试",
            entries = new[]
            {
                new { accountId = 2L, summary = "x", debitAmount = 500m, creditAmount = 0m, currency = "CNY" },
                new { accountId = 11L, summary = "y", debitAmount = 0m, creditAmount = 500m, currency = "CNY" },
            }
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var voucherId = createDoc!.RootElement.GetProperty("data").GetProperty("voucherId").GetInt64();

        var postResp = await _client.PutAsync($"/api/voucher/{voucherId}/post", null);
        var postDoc = await postResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        postDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        postDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("posted");
    }

    [Fact]
    public async Task PostVoucher_TwiceFails()
    {
        var createResp = await _client.PostAsJsonAsync("/api/voucher", new
        {
            voucherDate = DateTime.UtcNow,
            summary = "重复过账",
            entries = new[]
            {
                new { accountId = 2L, summary = "x", debitAmount = 100m, creditAmount = 0m, currency = "CNY" },
                new { accountId = 11L, summary = "y", debitAmount = 0m, creditAmount = 100m, currency = "CNY" },
            }
        });
        var voucherId = (await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("voucherId").GetInt64();

        await _client.PutAsync($"/api/voucher/{voucherId}/post", null);
        var resp2 = await _client.PutAsync($"/api/voucher/{voucherId}/post", null);
        var doc2 = await resp2.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc2!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task ClosePeriod_BlocksNewVoucher()
    {
        // 期间 1 在种子里是 closed 状态
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var closedPeriod = await db.Periods.FirstAsync(p => p.Status == "closed");

        var resp = await _client.PostAsJsonAsync("/api/voucher", new
        {
            periodId = closedPeriod.PeriodId,
            voucherDate = DateTime.UtcNow,
            summary = "关账期写入",
            entries = new[]
            {
                new { accountId = 2L, summary = "x", debitAmount = 100m, creditAmount = 0m, currency = "CNY" },
                new { accountId = 11L, summary = "y", debitAmount = 0m, creditAmount = 100m, currency = "CNY" },
            }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("关账");
    }

    [Fact]
    public async Task ClosePeriod_WithDraftVoucher_Returns400()
    {
        // 当前开放期间录入草稿 → 尝试关账应失败
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var openPeriod = await db.Periods.FirstAsync(p => p.Status == "open");

        await _client.PostAsJsonAsync("/api/voucher", new
        {
            periodId = openPeriod.PeriodId,
            voucherDate = DateTime.UtcNow,
            summary = "草稿凭证阻碍月结",
            entries = new[]
            {
                new { accountId = 2L, summary = "x", debitAmount = 100m, creditAmount = 0m, currency = "CNY" },
                new { accountId = 11L, summary = "y", debitAmount = 0m, creditAmount = 100m, currency = "CNY" },
            }
        });

        var resp = await _client.PutAsync($"/api/period/{openPeriod.PeriodId}/close", null);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("草稿");
    }
}
