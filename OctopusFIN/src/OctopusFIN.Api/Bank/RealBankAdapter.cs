using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OctopusFIN.Api.Bank;

namespace OctopusFIN.Api.Bank;

/// <summary>
/// 工商银行开放平台真实适配器（账务查询 P0015 + 银企互联）。
/// <para>凭证未配置时走确定性仿真模式（与 MockBankAdapter 行为一致），
/// 凭证到位后填入 appsettings.json 的 ICBC 配置节即自动切换为真实 API 调用。</para>
/// <para>接口模式：报文加签（数字签名验签），工行侧验签后返回结果。</para>
/// </summary>
public class RealBankAdapter(IHttpClientFactory httpFactory, IConfiguration config, ILogger<RealBankAdapter> logger) : IBankAdapter
{
    public string AdapterCode => "icbc";
    public bool IsMock => !IsConfigured;

    /// <summary>凭证是否已配置（AppID + AppSecret + BaseURL 三者齐全）</summary>
    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(config["ICBC:AppId"])
        && !string.IsNullOrWhiteSpace(config["ICBC:AppSecret"])
        && !string.IsNullOrWhiteSpace(config["ICBC:BaseUrl"]);

    private string AppId => config["ICBC:AppId"] ?? "";
    private string AppSecret => config["ICBC:AppSecret"] ?? "";
    private string BaseUrl => config["ICBC:BaseUrl"] ?? "https://open.icbc.com.cn";

    // ═══════════════════════════════════════════════════════════════
    // IBankAdapter 实现
    // ═══════════════════════════════════════════════════════════════

    public async Task<BankBalanceResult> GetBalanceAsync(string accountNo, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return SimulateBalance(accountNo);

        try
        {
            var payload = BuildSignedPayload(new { acctNo = accountNo, cmd = "balance" });
            var resp = await PostAsync("/icbc/account/balance", payload, ct);
            var doc = JsonDocument.Parse(resp);
            var success = doc.RootElement.GetProperty("returnCode").GetInt32() == 0;
            var balance = doc.RootElement.TryGetProperty("data", out var d)
                ? d.GetProperty("balance").GetDecimal()
                : 0m;
            return new BankBalanceResult(success, accountNo, balance, "CNY",
                success ? "工行余额查询成功" : "余额查询失败");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "工行余额查询失败 {AccountNo}", accountNo);
            return new BankBalanceResult(false, accountNo, 0, "CNY", $"查询异常：{ex.Message}");
        }
    }

    public async Task<List<BankStatementLine>> FetchStatementsAsync(
        string accountNo, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return SimulateStatements(accountNo, startDate, endDate);

        try
        {
            var payload = BuildSignedPayload(new
            {
                acctNo = accountNo,
                cmd = "statements",
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
            });
            var resp = await PostAsync("/icbc/account/statements", payload, ct);
            var doc = JsonDocument.Parse(resp);
            var lines = new List<BankStatementLine>();
            if (doc.RootElement.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    lines.Add(new BankStatementLine(
                        item.GetProperty("txnNo").GetString()!,
                        item.GetProperty("direction").GetString()!,
                        item.GetProperty("amount").GetDecimal(),
                        DateTime.Parse(item.GetProperty("txnDate").GetString()!),
                        item.TryGetProperty("counterparty", out var cp) ? cp.GetString() : null,
                        item.TryGetProperty("remark", out var rm) ? rm.GetString() : null,
                        item.GetProperty("externalTxnId").GetString()!));
                }
            }
            return lines;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "工行对账单拉取失败 {AccountNo}", accountNo);
            return new List<BankStatementLine>();
        }
    }

    public async Task<BankPaymentResult> PushPaymentAsync(BankPaymentRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return SimulatePayment(request);

        try
        {
            var payload = BuildSignedPayload(new
            {
                acctNo = request.AccountNo,
                cmd = "payment",
                direction = request.Direction,
                amount = request.Amount,
                counterparty = request.Counterparty,
                remark = request.Remark,
                txnDate = request.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss"),
            });
            var resp = await PostAsync("/icbc/payment/push", payload, ct);
            var doc = JsonDocument.Parse(resp);
            var success = doc.RootElement.GetProperty("returnCode").GetInt32() == 0;
            var externalId = doc.RootElement.TryGetProperty("data", out var d)
                ? d.GetProperty("orderId").GetString()
                : null;
            return new BankPaymentResult(success, externalId,
                success ? "工行收付款回写成功" : "回写失败");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "工行收付款回写失败 {AccountNo}", request.AccountNo);
            return new BankPaymentResult(false, null, $"回写异常：{ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 报文加签 + HTTP
    // ═══════════════════════════════════════════════════════════════

    /// <summary>构建报文并加签（HMAC-SHA256，工行侧验签）</summary>
    private string BuildSignedPayload(object body)
    {
        var json = JsonSerializer.Serialize(body);
        // 加签：HMAC-SHA256(AppSecret, json)，附在报文头
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(AppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
        var sign = Convert.ToBase64String(hash);
        return JsonSerializer.Serialize(new { appId = AppId, sign, body = json });
    }

    private async Task<string> PostAsync(string path, string payload, CancellationToken ct)
    {
        var client = httpFactory.CreateClient("icbc");
        client.BaseAddress = new Uri(BaseUrl);
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        var resp = await client.SendAsync(req, ct);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // 确定性仿真（凭证未配时，与 MockBankAdapter 行为一致）
    // ═══════════════════════════════════════════════════════════════

    private BankBalanceResult SimulateBalance(string accountNo)
    {
        var tail = accountNo.Length >= 4 ? accountNo[^4..] : accountNo;
        var balance = tail.Aggregate(0m, (acc, c) => acc + (decimal)(c - '0'));
        balance *= 1000;
        return new BankBalanceResult(true, accountNo, balance, "CNY",
            "工行适配器：凭证未配置，走确定性仿真");
    }

    private List<BankStatementLine> SimulateStatements(string accountNo, DateTime startDate, DateTime endDate) => new()
    {
        new("MOCK-TXN-001", "credit", 5000m, startDate, "客户A", "货款", $"MOCK-{accountNo}-001"),
        new("MOCK-TXN-002", "debit", 1200m, startDate.AddDays(5), "供应商B", "采购", $"MOCK-{accountNo}-002"),
        new("MOCK-TXN-003", "credit", 3000m, startDate.AddDays(10), "客户C", "货款", $"MOCK-{accountNo}-003"),
    };

    private BankPaymentResult SimulatePayment(BankPaymentRequest r) => new(
        Success: true,
        ExternalTxnId: $"MOCK-PAY-{r.AccountNo}-{r.TransactionDate:yyyyMMddHHmmss}",
        Message: "工行适配器：凭证未配置，走确定性仿真");
}
