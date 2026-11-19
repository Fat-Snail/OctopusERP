using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusFIN.Api.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusFIN.Api.Tests;

public class ArApAndWebhookTests : IClassFixture<FinTestFactory>
{
    private readonly HttpClient _client;
    private readonly FinTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ArApAndWebhookTests(FinTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // ── CRM 发货 → AR ──────────────────────────────────────────────────

    [Fact]
    public async Task CrmShipmentWebhook_GeneratesArInvoiceAndVoucher()
    {
        var resp = await _client.PostAsJsonAsync("/api/webhook/crm/shipment", new
        {
            crmContractId = 9001L,
            customerId = 1L,
            customerName = "测试客户A",
            amount = 50000m,
        });
        resp.IsSuccessStatusCode.Should().BeTrue();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("crmContractId").GetInt64().Should().Be(9001);
        data.GetProperty("amount").GetDecimal().Should().Be(50000m);
        data.GetProperty("status").GetString().Should().Be("open");
        data.GetProperty("voucherId").ValueKind.Should().NotBe(JsonValueKind.Null);

        // 凭证联动验证
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var voucherId = data.GetProperty("voucherId").GetInt64();
        var voucher = await db.Vouchers.Include(x => x.Entries).FirstAsync(v => v.VoucherId == voucherId);
        voucher.Source.Should().Be("crm-ar");
        voucher.Entries.Count.Should().Be(2);
        voucher.Entries.Sum(e => e.DebitAmount).Should().Be(50000m);
        voucher.Entries.Sum(e => e.CreditAmount).Should().Be(50000m);
        voucher.Entries.Should().Contain(e => e.AccountCode == "1122"); // 应收账款
        voucher.Entries.Should().Contain(e => e.AccountCode == "6001"); // 主营业务收入
    }

    [Fact]
    public async Task CrmShipmentWebhook_DuplicateContract_IsIdempotent()
    {
        await _client.PostAsJsonAsync("/api/webhook/crm/shipment", new
        {
            crmContractId = 9002L, customerId = 1L, customerName = "幂等客户", amount = 1000m,
        });
        await _client.PostAsJsonAsync("/api/webhook/crm/shipment", new
        {
            crmContractId = 9002L, customerId = 1L, customerName = "幂等客户", amount = 1000m,
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var count = await db.ArInvoices.CountAsync(x => x.CrmContractId == 9002);
        count.Should().Be(1);
    }

    [Fact]
    public async Task RegisterReceipt_PartialClosesPartialThenFull()
    {
        // 先生成 AR
        var arResp = await _client.PostAsJsonAsync("/api/webhook/crm/shipment", new
        {
            crmContractId = 9003L, customerId = 1L, customerName = "分期客户", amount = 1000m,
        });
        var invoiceId = (await arResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("invoiceId").GetInt64();

        // 部分收款
        var r1 = await _client.PostAsJsonAsync($"/api/ar/{invoiceId}/receipt", new { amount = 600m, paymentMethod = "银行转账", bankReference = "TX1" });
        (await r1.Content.ReadFromJsonAsync<JsonDocument>(_json))!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var detailResp = await _client.GetAsync($"/api/ar/{invoiceId}");
        var detailDoc = await detailResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        detailDoc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("partial");
        detailDoc.RootElement.GetProperty("data").GetProperty("receivedAmount").GetDecimal().Should().Be(600m);

        // 完整收款
        var r2 = await _client.PostAsJsonAsync($"/api/ar/{invoiceId}/receipt", new { amount = 400m, paymentMethod = "银行转账", bankReference = "TX2" });
        (await r2.Content.ReadFromJsonAsync<JsonDocument>(_json))!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        detailResp = await _client.GetAsync($"/api/ar/{invoiceId}");
        detailDoc = await detailResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        detailDoc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("closed");
    }

    [Fact]
    public async Task RegisterReceipt_OverAmount_Returns400()
    {
        var arResp = await _client.PostAsJsonAsync("/api/webhook/crm/shipment", new
        {
            crmContractId = 9004L, customerId = 1L, customerName = "超额客户", amount = 100m,
        });
        var invoiceId = (await arResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("invoiceId").GetInt64();

        var resp = await _client.PostAsJsonAsync($"/api/ar/{invoiceId}/receipt", new { amount = 500m, paymentMethod = "x", bankReference = "x" });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    // ── MES 入库 → AP ──────────────────────────────────────────────────

    [Fact]
    public async Task MesPurchaseWebhook_GeneratesApInvoiceAndVoucher()
    {
        var resp = await _client.PostAsJsonAsync("/api/webhook/mes/purchase-received", new
        {
            mesPurchaseOrderId = 8001L,
            supplierId = 1L,
            supplierName = "示例供应商1",
            amount = 250000m,
        });
        resp.IsSuccessStatusCode.Should().BeTrue();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var data = doc!.RootElement.GetProperty("data");
        data.GetProperty("amount").GetDecimal().Should().Be(250000m);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var voucherId = data.GetProperty("voucherId").GetInt64();
        var voucher = await db.Vouchers.Include(x => x.Entries).FirstAsync(v => v.VoucherId == voucherId);
        voucher.Source.Should().Be("mes-ap");
        voucher.Entries.Should().Contain(e => e.AccountCode == "1405"); // 库存商品
        voucher.Entries.Should().Contain(e => e.AccountCode == "2202"); // 应付账款
    }

    // ── OA 报销 → 费用凭证 ─────────────────────────────────────────────

    [Fact]
    public async Task OaExpenseWebhook_GeneratesExpenseVoucher()
    {
        var resp = await _client.PostAsJsonAsync("/api/webhook/oa/expense-approved", new
        {
            oaApprovalId = 7001L,
            amount = 5000m,
            summary = "差旅费报销 - 张三",
        });
        resp.IsSuccessStatusCode.Should().BeTrue();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("source").GetString().Should().Be("oa-expense");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var voucherId = data.GetProperty("voucherId").GetInt64();
        var voucher = await db.Vouchers.Include(x => x.Entries).FirstAsync(v => v.VoucherId == voucherId);
        voucher.Entries.Should().Contain(e => e.AccountCode == "6602"); // 管理费用
        voucher.Entries.Should().Contain(e => e.AccountCode == "1002"); // 银行存款
    }

    [Fact]
    public async Task OaExpenseWebhook_DuplicateApprovalId_IsIdempotent()
    {
        var payload = new { oaApprovalId = 7002L, amount = 888m, summary = "重复推送测试" };
        var first = await _client.PostAsJsonAsync("/api/webhook/oa/expense-approved", payload);
        var second = await _client.PostAsJsonAsync("/api/webhook/oa/expense-approved", payload);

        (await first.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("code").GetInt32().Should().Be(200);
        (await second.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("code").GetInt32().Should().Be(200);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var count = await db.Vouchers.CountAsync(v => v.Source == "oa-expense" && v.SourceRef == "7002");
        count.Should().Be(1, "同一审批 ID 重复推送只应生成一张费用凭证");
    }

    // ── 试算平衡表 ─────────────────────────────────────────────────────

    [Fact]
    public async Task TrialBalance_AfterPostedVouchers_IsBalanced()
    {
        // 触发 1 个 AR + 1 个 AP（两个都自动过账了么？— CreateAsync 是 draft，需要手动 post）
        // 创建并过账一笔
        var createResp = await _client.PostAsJsonAsync("/api/voucher", new
        {
            voucherDate = DateTime.UtcNow,
            summary = "TB 测试",
            entries = new[]
            {
                new { accountId = 2L, summary = "存款", debitAmount = 1234m, creditAmount = 0m, currency = "CNY" },
                new { accountId = 11L, summary = "收入", debitAmount = 0m, creditAmount = 1234m, currency = "CNY" },
            }
        });
        var voucherId = (await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("voucherId").GetInt64();
        await _client.PutAsync($"/api/voucher/{voucherId}/post", null);

        var resp = await _client.GetAsync("/api/reports/trial-balance");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("isBalanced").GetBoolean().Should().BeTrue();
        data.GetProperty("totalDebit").GetDecimal().Should().Be(data.GetProperty("totalCredit").GetDecimal());
        data.GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCurrentPeriod_ReturnsOpenPeriod()
    {
        var resp = await _client.GetAsync("/api/period/current");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("open");
    }

    // ── Q3-① CRM 回款 → 自动核销 AR + 收款凭证 ────────────────────────

    [Fact]
    public async Task PaymentReceivedWebhook_RegistersReceiptGeneratesVoucherAndCloses()
    {
        // 1. 发货确认 → 生成 AR（借 1122 应收 / 贷 6001 收入）
        var shipResp = await _client.PostAsJsonAsync("/api/webhook/crm/shipment", new
        {
            crmContractId = 9101L, customerId = 1L, customerName = "链路客户", amount = 50000m,
        });
        shipResp.IsSuccessStatusCode.Should().BeTrue();

        // 2. 回款确认（第一笔 30000）→ 核销 + 收款凭证（借 1002 银行 / 贷 1122 应收）
        var pay1 = await _client.PostAsJsonAsync("/api/webhook/crm/payment-received", new
        {
            crmContractId = 9101L, crmPaymentId = 501L, amount = 30000m,
            paymentMethod = "银行转账", bankReference = "TXN-A",
        });
        var p1 = await pay1.Content.ReadFromJsonAsync<JsonDocument>(_json);
        p1!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        p1.RootElement.GetProperty("msg").GetString().Should().Be("回款已核销（部分）");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
            var invoice = await db.ArInvoices.FirstAsync(x => x.CrmContractId == 9101);
            invoice.ReceivedAmount.Should().Be(30000m);
            invoice.Status.Should().Be("partial");

            var receipt = await db.ArReceipts.FirstAsync(x => x.InvoiceId == invoice.InvoiceId);
            receipt.VoucherId.Should().NotBeNull();
            var voucher = await db.Vouchers.Include(x => x.Entries)
                .FirstAsync(v => v.VoucherId == receipt.VoucherId);
            voucher.Source.Should().Be("crm-receipt");
            voucher.Entries.Count.Should().Be(2);
            voucher.Entries.Sum(e => e.DebitAmount).Should().Be(30000m);
            voucher.Entries.Sum(e => e.CreditAmount).Should().Be(30000m);
            voucher.Entries.Should().Contain(e => e.AccountCode == "1002"); // 银行存款
            voucher.Entries.Should().Contain(e => e.AccountCode == "1122"); // 应收账款冲减
        }

        // 3. 回款确认（第二笔 20000）→ 收齐自动结清
        var pay2 = await _client.PostAsJsonAsync("/api/webhook/crm/payment-received", new
        {
            crmContractId = 9101L, crmPaymentId = 502L, amount = 20000m,
            paymentMethod = "银行转账", bankReference = "TXN-B",
        });
        var p2 = await pay2.Content.ReadFromJsonAsync<JsonDocument>(_json);
        p2!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        p2.RootElement.GetProperty("msg").GetString().Should().Be("回款已核销，应收已结清");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
            var invoice = await db.ArInvoices.FirstAsync(x => x.CrmContractId == 9101);
            invoice.Status.Should().Be("closed");
            invoice.ReceivedAmount.Should().Be(invoice.Amount);

            // 复式记账仍平衡
            var vouchers = await db.Vouchers.Include(x => x.Entries)
                .Where(v => v.Source == "crm-ar" || v.Source == "crm-receipt").ToListAsync();
            vouchers.Sum(v => v.Entries.Sum(e => e.DebitAmount))
                .Should().Be(vouchers.Sum(v => v.Entries.Sum(e => e.CreditAmount)));
        }
    }

    [Fact]
    public async Task PaymentReceivedWebhook_DuplicatePaymentId_IsIdempotent()
    {
        await _client.PostAsJsonAsync("/api/webhook/crm/shipment", new
        {
            crmContractId = 9102L, customerId = 1L, customerName = "幂等回款客户", amount = 10000m,
        });

        var payload = new { crmContractId = 9102L, crmPaymentId = 601L, amount = 4000m, paymentMethod = "银行转账" };
        var first = await _client.PostAsJsonAsync("/api/webhook/crm/payment-received", payload);
        var second = await _client.PostAsJsonAsync("/api/webhook/crm/payment-received", payload);

        (await first.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var secondDoc = await second.Content.ReadFromJsonAsync<JsonDocument>(_json);
        secondDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        secondDoc.RootElement.GetProperty("msg").GetString().Should().Contain("幂等");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var invoice = await db.ArInvoices.FirstAsync(x => x.CrmContractId == 9102);
        invoice.ReceivedAmount.Should().Be(4000m); // 只核销一次
        var receipts = await db.ArReceipts.Where(x => x.InvoiceId == invoice.InvoiceId).CountAsync();
        receipts.Should().Be(1);
    }

    [Fact]
    public async Task PaymentReceivedWebhook_UnknownContract_ReturnsBusinessError()
    {
        var resp = await _client.PostAsJsonAsync("/api/webhook/crm/payment-received", new
        {
            crmContractId = 99999L, crmPaymentId = 701L, amount = 100m, paymentMethod = "银行转账",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("尚无应收发票");
    }
}
