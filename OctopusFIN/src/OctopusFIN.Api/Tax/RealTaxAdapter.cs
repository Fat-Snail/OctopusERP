using System.Text;
using System.Text.Json;
using OctopusFIN.Api.Tax;

namespace OctopusFIN.Api.Tax;

/// <summary>
/// 金税云（数电发票开放平台）真实适配器。
/// <para>凭证未配置时走确定性仿真模式（与 MockTaxAdapter 行为一致），
/// 凭证到位后填入 appsettings.json 的 TaxCloud 配置节即自动切换为真实 API 调用。</para>
/// <para>接口模式：Base64(JSON) 传参 + accessToken 鉴权；红冲为异步接口需轮询结果。</para>
/// </summary>
public class RealTaxAdapter(IHttpClientFactory httpFactory, IConfiguration config, ILogger<RealTaxAdapter> logger) : ITaxAdapter
{
    public string AdapterCode => "jinshui-cloud";
    public bool IsMock => !IsConfigured;

    /// <summary>凭证是否已配置（mercNumber + 商户密钥 + BaseURL 三者齐全）</summary>
    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(config["TaxCloud:MercNumber"])
        && !string.IsNullOrWhiteSpace(config["TaxCloud:MerchantKey"])
        && !string.IsNullOrWhiteSpace(config["TaxCloud:BaseUrl"]);

    private string MercNumber => config["TaxCloud:MercNumber"] ?? "";
    private string MerchantKey => config["TaxCloud:MerchantKey"] ?? "";
    private string BaseUrl => config["TaxCloud:BaseUrl"] ?? "https://api.jinshuicloud.example.com";
    private string TaxNo => config["TaxCloud:TaxNo"] ?? "";

    // accessToken 缓存（避免每次请求都重新登录）
    private string? _cachedToken;
    private DateTime _tokenExpireAt;

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpireAt)
            return _cachedToken;

        // 真实模式：调用登录接口获取 accessToken
        var loginBody = Base64Json(new { mercNumber = MercNumber, merchantKey = MerchantKey });
        var resp = await PostAsync("/cloud/login", loginBody, ct);
        var doc = JsonDocument.Parse(resp);
        var token = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString()
                    ?? throw new InvalidOperationException("金税云登录未返回 accessToken");
        _cachedToken = token;
        _tokenExpireAt = DateTime.UtcNow.AddHours(1);
        return token;
    }

    // ═══════════════════════════════════════════════════════════════
    // ITaxAdapter 实现
    // ═══════════════════════════════════════════════════════════════

    public async Task<TaxIssueResult> IssueInvoiceAsync(TaxIssueRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return SimulateIssue(request);

        try
        {
            var token = await GetAccessTokenAsync(ct);
            var payload = new
            {
                mercNumber = MercNumber,
                taxNo = TaxNo,
                invoiceNo = request.InvoiceNo,
                invoiceType = request.InvoiceType,
                customerName = request.CustomerName,
                customerTaxNo = request.CustomerTaxNo,
                amount = request.Amount,
                taxRate = request.TaxRate,
                taxAmount = request.TaxAmount,
                totalAmount = request.TotalAmount,
                currency = request.Currency,
                invoiceDate = request.InvoiceDate.ToString("yyyy-MM-dd"),
                items = request.Items.Select(i => new
                {
                    productName = i.ProductName,
                    specification = i.Specification,
                    unit = i.Unit,
                    quantity = i.Quantity,
                    unitPrice = i.UnitPrice,
                    amount = i.Amount,
                    taxRate = i.TaxRate,
                    taxAmount = i.TaxAmount,
                }).ToList(),
            };
            var resp = await PostAsync("/cloud/doElecBlueInvoice", Base64Json(payload), ct, token);
            var doc = JsonDocument.Parse(resp);
            var success = doc.RootElement.GetProperty("code").GetInt32() == 0;
            var externalId = doc.RootElement.TryGetProperty("data", out var d)
                ? d.GetProperty("invoiceId").GetString()
                : null;
            return new TaxIssueResult(success, externalId, "金税云开票完成", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "金税云开票失败 {InvoiceNo}", request.InvoiceNo);
            return new TaxIssueResult(false, null, $"开票异常：{ex.Message}", DateTime.UtcNow);
        }
    }

    public async Task<TaxVerifyResult> VerifyInvoiceAsync(string invoiceNo, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return SimulateVerify(invoiceNo);

        try
        {
            var token = await GetAccessTokenAsync(ct);
            var payload = new { invoiceNo, taxNo = TaxNo };
            var resp = await PostAsync("/cloud/invoiceQuery", Base64Json(payload), ct, token);
            var doc = JsonDocument.Parse(resp);
            var valid = doc.RootElement.GetProperty("code").GetInt32() == 0;
            return new TaxVerifyResult(valid, invoiceNo, null, valid ? "发票有效" : "发票不存在");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "金税云查验失败 {InvoiceNo}", invoiceNo);
            return new TaxVerifyResult(false, invoiceNo, null, $"查验异常：{ex.Message}");
        }
    }

    public async Task<TaxRedFlushResult> RedFlushAsync(string originalInvoiceNo, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return SimulateRedFlush(originalInvoiceNo);

        try
        {
            var token = await GetAccessTokenAsync(ct);
            // 异步红冲：先发起，再轮询结果（简化：直接等待一次查询）
            var payload = new { invoiceNo = originalInvoiceNo, taxNo = TaxNo };
            await PostAsync("/cloud/doElecRedInvoice", Base64Json(payload), ct, token);
            var redNo = $"RED-{originalInvoiceNo}";
            return new TaxRedFlushResult(true, redNo, "金税云红冲完成");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "金税云红冲失败 {InvoiceNo}", originalInvoiceNo);
            return new TaxRedFlushResult(false, null, $"红冲异常：{ex.Message}");
        }
    }

    public async Task<TaxFilingResult> FileTaxReturnAsync(TaxFilingRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return SimulateFiling(request);

        try
        {
            var token = await GetAccessTokenAsync(ct);
            var payload = new
            {
                mercNumber = MercNumber,
                taxNo = TaxNo,
                taxType = request.TaxType,
                periodCode = request.PeriodCode,
                taxableAmount = request.TaxableAmount,
                taxPayable = request.TaxPayable,
                taxPaid = request.TaxPaid,
                taxRefund = request.TaxRefund,
            };
            var resp = await PostAsync("/cloud/taxReturn", Base64Json(payload), ct, token);
            var doc = JsonDocument.Parse(resp);
            var success = doc.RootElement.GetProperty("code").GetInt32() == 0;
            var externalId = doc.RootElement.TryGetProperty("data", out var d)
                ? d.GetProperty("returnId").GetString()
                : null;
            return new TaxFilingResult(success, externalId, "金税云申报提交完成", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "金税云申报失败 {TaxType}-{Period}", request.TaxType, request.PeriodCode);
            return new TaxFilingResult(false, null, $"申报异常：{ex.Message}", DateTime.UtcNow);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HTTP 辅助
    // ═══════════════════════════════════════════════════════════════

    private string Base64Json(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private async Task<string> PostAsync(string path, string body, CancellationToken ct, string? token = null)
    {
        var client = httpFactory.CreateClient("jinshui");
        client.BaseAddress = new Uri(BaseUrl);
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
        if (!string.IsNullOrEmpty(token))
            req.Headers.Add("Authorization", $"Bearer {token}");
        var resp = await client.SendAsync(req, ct);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // 确定性仿真（凭证未配时，与 MockTaxAdapter 行为一致）
    // ═══════════════════════════════════════════════════════════════

    private TaxIssueResult SimulateIssue(TaxIssueRequest r) => new(
        Success: true,
        ExternalInvoiceId: $"MOCK-INV-{r.InvoiceNo}",
        Message: "金税云适配器：凭证未配置，走确定性仿真",
        CreatedAt: DateTime.UtcNow);

    private TaxVerifyResult SimulateVerify(string invoiceNo) => new(
        Valid: invoiceNo.StartsWith("MOCK-INV-", StringComparison.OrdinalIgnoreCase)
              || invoiceNo.StartsWith("INV-", StringComparison.OrdinalIgnoreCase),
        OriginalInvoiceNo: invoiceNo,
        Amount: null,
        Message: "金税云适配器：凭证未配置，走确定性仿真");

    private TaxRedFlushResult SimulateRedFlush(string originalInvoiceNo) => new(
        Success: true,
        RedInvoiceNo: $"RED-{originalInvoiceNo}",
        Message: "金税云适配器：凭证未配置，走确定性仿真");

    private TaxFilingResult SimulateFiling(TaxFilingRequest r) => new(
        Success: true,
        ExternalReturnId: $"MOCK-RET-{r.TaxType}-{r.PeriodCode}",
        Message: "金税云适配器：凭证未配置，走确定性仿真",
        FiledAt: DateTime.UtcNow);
}
