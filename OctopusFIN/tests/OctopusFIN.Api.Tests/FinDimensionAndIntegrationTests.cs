using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusFIN.Api.Tests;

/// <summary>
/// 阶段一四任务集成测试：
/// 1. 多组织/多利润中心切分  2. 三大报表深化  3. 税务与发票  4. 银企直连
/// </summary>
public class FinDimensionAndIntegrationTests : IClassFixture<FinTestFactory>
{
    private readonly HttpClient _client;
    private readonly FinTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FinDimensionAndIntegrationTests(FinTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // ═══════════════════════════════════════════════════════════════
    // 任务1：多组织/多利润中心切分
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ProfitCenter_List_ReturnsSeededCenters()
    {
        var resp = await _client.GetAsync("/api/profit-center");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("rows").GetArrayLength().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task ProfitCenter_CreateAndDelete_Lifecycle()
    {
        // 创建
        var createResp = await _client.PostAsJsonAsync("/api/profit-center", new
        {
            centerCode = "PC-TEST",
            centerName = "测试利润中心",
            organizationId = 1L,
            status = "active",
            remark = "测试",
        });
        var doc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var pcId = doc.RootElement.GetProperty("data").GetProperty("profitCenterId").GetInt64();

        // 删除
        var delResp = await _client.DeleteAsync($"/api/profit-center/{pcId}");
        var delDoc = await delResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        delDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task ProfitCenter_DeleteReferenced_ReturnsError()
    {
        // 种子利润中心 PC-SALES（id=1）已被凭证引用，不可删除
        var resp = await _client.DeleteAsync("/api/profit-center/1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("不可删除");
    }

    [Fact]
    public async Task Voucher_WithProfitCenter_IsStored()
    {
        // 录入一笔带利润中心的凭证并过账
        var createResp = await _client.PostAsJsonAsync("/api/voucher", new
        {
            voucherDate = DateTime.UtcNow,
            summary = "利润中心凭证",
            organizationId = 1L,
            entries = new[]
            {
                new { accountId = 2L, summary = "存款", debitAmount = 5000m, creditAmount = 0m, currency = "CNY", profitCenterId = 1L },
                new { accountId = 11L, summary = "收入", debitAmount = 0m, creditAmount = 5000m, currency = "CNY", profitCenterId = 1L },
            }
        });
        var doc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var voucherId = doc.RootElement.GetProperty("data").GetProperty("voucherId").GetInt64();

        await _client.PutAsync($"/api/voucher/{voucherId}/post", null);

        // 查询详情，确认利润中心已写入
        var detailResp = await _client.GetAsync($"/api/voucher/{voucherId}");
        var detailDoc = await detailResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        detailDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var entries = detailDoc.RootElement.GetProperty("data").GetProperty("entries");
        entries.GetArrayLength().Should().Be(2);
    }

    // ═══════════════════════════════════════════════════════════════
    // 任务2：三大报表深化
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task BalanceSheet_ReturnsAssetsAndLiabilities()
    {
        var resp = await _client.GetAsync("/api/reports/balance-sheet");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("assets").GetArrayLength().Should().BeGreaterThan(0);
        data.GetProperty("isBalanced").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task IncomeStatement_ReturnsRevenueAndExpense()
    {
        var resp = await _client.GetAsync("/api/reports/income-statement");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("revenueTotal", out _).Should().BeTrue();
        data.TryGetProperty("netProfit", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CashFlow_ReturnsThreeCategories()
    {
        var resp = await _client.GetAsync("/api/reports/cash-flow");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("operating", out _).Should().BeTrue();
        data.TryGetProperty("investing", out _).Should().BeTrue();
        data.TryGetProperty("financing", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AmoebaReport_ReturnsPerProfitCenter()
    {
        var resp = await _client.GetAsync("/api/reports/amoeba");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("rows").GetArrayLength().Should().BeGreaterThanOrEqualTo(3);
        data.TryGetProperty("totalRevenue", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TrialBalance_WithProfitCenterFilter_Works()
    {
        var resp = await _client.GetAsync("/api/reports/trial-balance?profitCenterId=1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }

    // ═══════════════════════════════════════════════════════════════
    // 任务3：税务与发票
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Tax_AdapterInfo_ReturnsMock()
    {
        var resp = await _client.GetAsync("/api/tax/adapter");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("isMock").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Tax_IssueInvoice_CreatesAndReturnsExternalId()
    {
        var resp = await _client.PostAsJsonAsync("/api/tax/invoices", new
        {
            customerName = "测试客户",
            customerTaxNo = "91310000TEST",
            amount = 10000m,
            taxRate = 0.13m,
            currency = "CNY",
            items = new[]
            {
                new { productName = "商品A", specification = "标准", unit = "件", quantity = 10, unitPrice = 1000m, amount = 10000m, taxRate = 0.13m },
            }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("invoiceNo").GetString().Should().StartWith("INV-");
        data.GetProperty("externalInvoiceId").GetString().Should().StartWith("MOCK-INV-");
        data.GetProperty("totalAmount").GetDecimal().Should().Be(11300m);
    }

    [Fact]
    public async Task Tax_VerifyInvoice_ReturnsValid()
    {
        var resp = await _client.PostAsync("/api/tax/invoices/INV-20261128-TEST/verify", null);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        // Mock 沙箱：以 INV- 开头视为有效
        doc!.RootElement.GetProperty("data").GetProperty("valid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Tax_RedFlush_CreatesRedInvoice()
    {
        // 先开具一张发票，再红冲
        var createResp = await _client.PostAsJsonAsync("/api/tax/invoices", new
        {
            customerName = "红冲测试客户",
            amount = 5000m,
            taxRate = 0.13m,
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var invoiceNo = createDoc!.RootElement.GetProperty("data").GetProperty("invoiceNo").GetString();

        var resp = await _client.PostAsync($"/api/tax/invoices/{invoiceNo}/red-flush", null);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task Tax_FileReturn_SubmitsToAdapter()
    {
        var resp = await _client.PostAsJsonAsync("/api/tax/returns", new
        {
            taxType = "vat",
            periodCode = "2026-11",
            periodId = 2L,
            taxableAmount = 100000m,
            taxPayable = 13000m,
            taxPaid = 13000m,
            taxRefund = 0m,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("filed");
        data.GetProperty("externalReturnId").GetString().Should().StartWith("MOCK-RET-");
    }

    // ═══════════════════════════════════════════════════════════════
    // 任务4：银企直连
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bank_AdapterInfo_ReturnsMock()
    {
        var resp = await _client.GetAsync("/api/bank/adapter");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("isMock").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Bank_CreateAccount_AndGetBalance()
    {
        var createResp = await _client.PostAsJsonAsync("/api/bank/accounts", new
        {
            accountNo = "6228480000001234",
            accountName = "测试账户",
            bankName = "测试银行",
            organizationId = 1L,
            status = "active",
        });
        var doc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var accountId = doc.RootElement.GetProperty("data").GetProperty("accountId").GetInt64();

        var balResp = await _client.GetAsync($"/api/bank/accounts/{accountId}/balance");
        var balDoc = await balResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        balDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        balDoc.RootElement.GetProperty("data").GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Bank_FetchStatements_IsIdempotent()
    {
        // 创建账户
        var createResp = await _client.PostAsJsonAsync("/api/bank/accounts", new
        {
            accountNo = "6228480000005678",
            accountName = "对账账户",
            bankName = "测试银行",
        });
        var doc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var accountId = doc!.RootElement.GetProperty("data").GetProperty("accountId").GetInt64();

        // 第一次拉取
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);
        var resp1 = await _client.PostAsJsonAsync(
            $"/api/bank/accounts/{accountId}/statements/fetch?startDate={start:o}&endDate={end:o}", new { });
        var doc1 = await resp1.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc1!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var fetched1 = doc1.RootElement.GetProperty("data").GetProperty("fetched").GetInt32();

        // 第二次拉取（幂等：应跳过已存在的）
        var resp2 = await _client.PostAsJsonAsync(
            $"/api/bank/accounts/{accountId}/statements/fetch?startDate={start:o}&endDate={end:o}", new { });
        var doc2 = await resp2.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc2!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var fetched2 = doc2.RootElement.GetProperty("data").GetProperty("fetched").GetInt32();
        var skipped2 = doc2.RootElement.GetProperty("data").GetProperty("skipped").GetInt32();

        fetched1.Should().Be(3);
        fetched2.Should().Be(0);
        skipped2.Should().Be(3);
    }

    [Fact]
    public async Task Bank_AutoReconcile_MatchesStatements()
    {
        // 创建账户 + 拉取流水 + 自动对账
        var createResp = await _client.PostAsJsonAsync("/api/bank/accounts", new
        {
            accountNo = "6228480000009999",
            accountName = "对账测试",
            bankName = "测试银行",
        });
        var doc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var accountId = doc!.RootElement.GetProperty("data").GetProperty("accountId").GetInt64();

        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);
        await _client.PostAsJsonAsync(
            $"/api/bank/accounts/{accountId}/statements/fetch?startDate={start:o}&endDate={end:o}", new { });

        var recResp = await _client.PostAsync($"/api/bank/accounts/{accountId}/reconcile", null);
        var recDoc = await recResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        recDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        recDoc.RootElement.GetProperty("data").GetProperty("matched").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }
}
