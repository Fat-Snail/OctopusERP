using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusFIN.Api.Persistence;
using OctopusFIN.Api.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusFIN.Api.Tests;

/// <summary>Q3-③ 月结流程：结转损益 + 期间锁定 + 月结报告</summary>
public class ClosingTests : IClassFixture<FinTestFactory>
{
    private readonly HttpClient _client;
    private readonly FinTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ClosingTests(FinTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    /// <summary>自建独立期间，避免测试间共享 seed 期间的状态</summary>
    private async Task<FinPeriod> NewOpenPeriodAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var p = new FinPeriod { PeriodCode = code, Year = int.Parse(code[..4]), Month = int.Parse(code[5..]), Status = "open" };
        db.Periods.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    private static async Task<Dictionary<string, long>> GetAccountIdsAsync(FinDbContext db) =>
        await db.Accounts.ToDictionaryAsync(a => a.AccountCode, a => a.AccountId);

    /// <summary>录入并过账一张凭证</summary>
    private static async Task<FinVoucher> CreateAndPostAsync(VoucherService voucherSvc, FinDbContext db,
        long periodId, long debitCode, long creditCode, decimal amount, string source = "manual")
    {
        var ids = await GetAccountIdsAsync(db);
        var (voucher, err) = await voucherSvc.CreateAsync(new VoucherInput(
            periodId, DateTime.UtcNow, "normal",
            $"测试凭证 {debitCode}/{creditCode}", source, null,
            new List<VoucherEntryInput>
            {
                new(ids[$"{debitCode}"], "借方", amount, 0, "CNY"),
                new(ids[$"{creditCode}"], "贷方", 0, amount, "CNY"),
            }), 1);
        err.Should().BeNull();
        voucher!.Status.Should().Be("draft");

        var (posted, postErr) = await voucherSvc.PostAsync(voucher.VoucherId);
        postErr.Should().BeNull();
        return posted!;
    }

    [Fact]
    public async Task ClosePeriod_TransfersProfit_CreatesPostedClosingVoucher()
    {
        var period = await NewOpenPeriodAsync("2099-01");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
            var voucherSvc = scope.ServiceProvider.GetRequiredService<VoucherService>();

            // 收入 10000（借 银行存款 / 贷 主营业务收入）+ 费用 3000（借 管理费用 / 贷 银行存款）
            await CreateAndPostAsync(voucherSvc, db, period.PeriodId, 1002, 6001, 10000m);
            await CreateAndPostAsync(voucherSvc, db, period.PeriodId, 6602, 1002, 3000m);

            var periodSvc = scope.ServiceProvider.GetRequiredService<PeriodService>();
            var (closed, err) = await periodSvc.CloseAsync(period.PeriodId, 1);
            err.Should().BeNull();
            closed!.Status.Should().Be("closed");

            // 结转凭证：posted、4 行分录、借贷各 13000
            var closing = await db.Vouchers.Include(v => v.Entries)
                .FirstAsync(v => v.PeriodId == period.PeriodId && v.Source == "closing");
            closing.Status.Should().Be("posted");
            closing.VoucherType.Should().Be("closing");
            closing.Entries.Count.Should().Be(4);
            closing.TotalDebit.Should().Be(13000m);
            closing.TotalCredit.Should().Be(13000m);
            closing.Entries.Should().Contain(e => e.AccountCode == "6001" && e.DebitAmount == 10000m); // 收入结转
            closing.Entries.Should().Contain(e => e.AccountCode == "4103" && e.CreditAmount == 10000m); // 转入本年利润
            closing.Entries.Should().Contain(e => e.AccountCode == "4103" && e.DebitAmount == 3000m);  // 费用转入
            closing.Entries.Should().Contain(e => e.AccountCode == "6602" && e.CreditAmount == 3000m); // 费用结转
        }
    }

    [Fact]
    public async Task MonthEndReport_AfterClosing_ShowsProfitAndBalances()
    {
        var period = await NewOpenPeriodAsync("2099-02");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
            var voucherSvc = scope.ServiceProvider.GetRequiredService<VoucherService>();
            await CreateAndPostAsync(voucherSvc, db, period.PeriodId, 1002, 6001, 10000m);
            await CreateAndPostAsync(voucherSvc, db, period.PeriodId, 6602, 1002, 3000m);
            await scope.ServiceProvider.GetRequiredService<PeriodService>().CloseAsync(period.PeriodId, 1);
        }

        var resp = await _client.GetAsync($"/api/reports/month-end-closing/{period.PeriodId}");
        resp.IsSuccessStatusCode.Should().BeTrue();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("periodCode").GetString().Should().Be("2099-02");
        data.GetProperty("periodStatus").GetString().Should().Be("closed");
        data.GetProperty("revenueTotal").GetDecimal().Should().Be(10000m);
        data.GetProperty("expenseTotal").GetDecimal().Should().Be(3000m);
        data.GetProperty("netProfit").GetDecimal().Should().Be(7000m);
        data.GetProperty("closingVoucherNo").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("trialBalanceOk").GetBoolean().Should().BeTrue();

        // 结转后余额快照：损益类清零、本年利润 = 7000（贷）
        var balances = data.GetProperty("balanceSnapshot").EnumerateArray().ToList();
        var profit = balances.Single(b => b.GetProperty("accountCode").GetString() == "4103");
        profit.GetProperty("balance").GetDecimal().Should().Be(7000m);
        profit.GetProperty("direction").GetString().Should().Be("credit");
        balances.Single(b => b.GetProperty("accountCode").GetString() == "6001")
            .GetProperty("balance").GetDecimal().Should().Be(0m);
        balances.Single(b => b.GetProperty("accountCode").GetString() == "6602")
            .GetProperty("balance").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task ClosePeriod_NoActivity_ClosesWithoutClosingVoucher()
    {
        var period = await NewOpenPeriodAsync("2099-03");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var periodSvc = scope.ServiceProvider.GetRequiredService<PeriodService>();

        var (closed, err) = await periodSvc.CloseAsync(period.PeriodId, 1);
        err.Should().BeNull();
        closed!.Status.Should().Be("closed");

        var closingCount = await db.Vouchers.CountAsync(v => v.PeriodId == period.PeriodId && v.Source == "closing");
        closingCount.Should().Be(0, "本期无收入/费用发生，不应生成结转凭证");
    }

    [Fact]
    public async Task ClosePeriod_WithDraftVoucher_Rejected()
    {
        var period = await NewOpenPeriodAsync("2099-04");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var voucherSvc = scope.ServiceProvider.GetRequiredService<VoucherService>();
        var ids = await GetAccountIdsAsync(db);

        // 留一张草稿凭证
        var (draft, err) = await voucherSvc.CreateAsync(new VoucherInput(
            period.PeriodId, DateTime.UtcNow, "normal", "未过账草稿",
            "manual", null,
            new List<VoucherEntryInput>
            {
                new(ids["1002"], "借方", 100m, 0, "CNY"),
                new(ids["6001"], "贷方", 0, 100m, "CNY"),
            }), 1);
        err.Should().BeNull();

        var periodSvc = scope.ServiceProvider.GetRequiredService<PeriodService>();
        var (_, closeErr) = await periodSvc.CloseAsync(period.PeriodId, 1);
        closeErr.Should().Contain("草稿凭证");

        // 期间仍为 open，草稿仍在
        var reload = await db.Periods.FirstAsync(p => p.PeriodId == period.PeriodId);
        reload.Status.Should().Be("open");
    }

    [Fact]
    public async Task ClosedPeriod_RejectsNewVoucher()
    {
        var period = await NewOpenPeriodAsync("2099-05");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var voucherSvc = scope.ServiceProvider.GetRequiredService<VoucherService>();
        var periodSvc = scope.ServiceProvider.GetRequiredService<PeriodService>();

        (await periodSvc.CloseAsync(period.PeriodId, 1)).error.Should().BeNull();

        var ids = await GetAccountIdsAsync(db);
        var (_, createErr) = await voucherSvc.CreateAsync(new VoucherInput(
            period.PeriodId, DateTime.UtcNow, "normal", "关账后录入",
            "manual", null,
            new List<VoucherEntryInput>
            {
                new(ids["1002"], "借方", 100m, 0, "CNY"),
                new(ids["6001"], "贷方", 0, 100m, "CNY"),
            }), 1);
        createErr.Should().Contain("已关账");
    }

    [Fact]
    public async Task TransferProfit_BeforeClose_IsReusedByClose()
    {
        var period = await NewOpenPeriodAsync("2099-06");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinDbContext>();
        var voucherSvc = scope.ServiceProvider.GetRequiredService<VoucherService>();
        var closingSvc = scope.ServiceProvider.GetRequiredService<ClosingService>();
        var periodSvc = scope.ServiceProvider.GetRequiredService<PeriodService>();

        await CreateAndPostAsync(voucherSvc, db, period.PeriodId, 1002, 6001, 8000m);

        // 手工预结转一次
        var (first, err1) = await closingSvc.TransferProfitAsync(period.PeriodId, 1);
        err1.Should().BeNull();
        first!.Status.Should().Be("posted");

        // 再关账：CloseAsync 内部再次结转 → 幂等复用同一张凭证，不重复
        var (closed, err2) = await periodSvc.CloseAsync(period.PeriodId, 1);
        err2.Should().BeNull();
        closed!.Status.Should().Be("closed");

        var closings = await db.Vouchers.Where(v => v.PeriodId == period.PeriodId && v.Source == "closing").CountAsync();
        closings.Should().Be(1);
    }
}
