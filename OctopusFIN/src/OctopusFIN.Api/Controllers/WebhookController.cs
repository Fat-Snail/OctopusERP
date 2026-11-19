using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusFIN.Api.Controllers;

/// <summary>P13-3：跨系统 Webhook 接收（CRM 发货 → AR / MES 入库 → AP / OA 报销 → 费用）</summary>
[ApiController]
[Route("api/webhook")]
public class WebhookController(
    ArService arSvc,
    ApService apSvc,
    IConfiguration config) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>CRM 合同发货 → 自动生成 AR 发票 + 凭证</summary>
    [HttpPost("crm/shipment")]
    public async Task<IActionResult> CrmShipment()
    {
        var (body, ok) = await VerifyAndReadAsync("X-Fin-Signature", config["Webhooks:FinSecret"]);
        if (!ok) return Unauthorized(new { code = 401, msg = "签名验证失败" });

        var req = JsonSerializer.Deserialize<CrmShipmentRequest>(body, JsonOpts);
        if (req == null) return BadRequest(new { code = 400, msg = "请求体格式错误" });

        var (invoice, err) = await arSvc.CreateFromShipmentAsync(
            req.CrmContractId, req.CustomerId, req.CustomerName ?? "未知客户", req.Amount, 1);
        if (err != null) return Ok(new { code = 400, msg = err, data = (object?)null });
        return Ok(new { code = 200, msg = "AR 已生成", data = invoice });
    }

    /// <summary>MES 采购入库 → 自动生成 AP 发票 + 凭证</summary>
    [HttpPost("mes/purchase-received")]
    public async Task<IActionResult> MesPurchaseReceived()
    {
        var (body, ok) = await VerifyAndReadAsync("X-Fin-Signature", config["Webhooks:FinSecret"]);
        if (!ok) return Unauthorized(new { code = 401, msg = "签名验证失败" });

        var req = JsonSerializer.Deserialize<MesPurchaseRequest>(body, JsonOpts);
        if (req == null) return BadRequest(new { code = 400, msg = "请求体格式错误" });

        var (invoice, err) = await apSvc.CreateFromPurchaseAsync(
            req.MesPurchaseOrderId, req.SupplierId, req.SupplierName ?? "未知供应商", req.Amount, 1);
        if (err != null) return Ok(new { code = 400, msg = err, data = (object?)null });
        return Ok(new { code = 200, msg = "AP 已生成", data = invoice });
    }

    /// <summary>OA 报销审批通过 → 自动生成费用凭证</summary>
    [HttpPost("oa/expense-approved")]
    public async Task<IActionResult> OaExpenseApproved()
    {
        var (body, ok) = await VerifyAndReadAsync("X-Fin-Signature", config["Webhooks:FinSecret"]);
        if (!ok) return Unauthorized(new { code = 401, msg = "签名验证失败" });

        var req = JsonSerializer.Deserialize<OaExpenseRequest>(body, JsonOpts);
        if (req == null) return BadRequest(new { code = 400, msg = "请求体格式错误" });

        var (voucher, err) = await apSvc.CreateFromExpenseAsync(
            req.OaApprovalId, req.Amount, req.Summary, 1);
        if (err != null) return Ok(new { code = 400, msg = err, data = (object?)null });
        return Ok(new { code = 200, msg = "费用凭证已生成", data = voucher });
    }

    /// <summary>CRM 回款确认 → 自动核销 AR（收款凭证由 RegisterReceiptAsync 内部生成）</summary>
    [HttpPost("crm/payment-received")]
    public async Task<IActionResult> CrmPaymentReceived()
    {
        var (body, ok) = await VerifyAndReadAsync("X-Fin-Signature", config["Webhooks:FinSecret"]);
        if (!ok) return Unauthorized(new { code = 401, msg = "签名验证失败" });

        var req = JsonSerializer.Deserialize<CrmPaymentRequest>(body, JsonOpts);
        if (req == null) return BadRequest(new { code = 400, msg = "请求体格式错误" });

        // 幂等：同一笔回款（CRM paymentId）重复推送只核销一次
        var externalRef = $"crm-pay-{req.CrmPaymentId}";
        var existingReceipt = await arSvc.FindReceiptByExternalRefAsync(externalRef);
        if (existingReceipt != null)
            return Ok(new { code = 200, msg = "该回款已核销（幂等跳过）", data = existingReceipt });

        var invoice = await arSvc.GetByContractIdAsync(req.CrmContractId);
        if (invoice == null)
            return Ok(new { code = 400, msg = $"合同 {req.CrmContractId} 尚无应收发票（先经发货确认生成 AR）", data = (object?)null });
        if (invoice.Status == "closed")
            return Ok(new { code = 200, msg = "应收已结清", data = invoice });

        var (receipt, err) = await arSvc.RegisterReceiptAsync(
            invoice.InvoiceId, req.Amount, req.PaymentMethod ?? "bank",
            string.IsNullOrEmpty(req.BankReference) ? externalRef : $"{req.BankReference}|{externalRef}", 1);
        if (err != null) return Ok(new { code = 400, msg = err, data = (object?)null });

        return Ok(new
        {
            code = 200,
            msg = invoice.Status == "closed" ? "回款已核销，应收已结清" : "回款已核销（部分）",
            data = receipt,
        });
    }

    private async Task<(string body, bool valid)> VerifyAndReadAsync(string sigHeader, string? secret)
    {
        Request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        // 测试/未配置 secret 时跳过签名校验
        if (string.IsNullOrEmpty(secret)) return (body, true);

        var sig = Request.Headers[sigHeader].ToString();
        var expected = "sha256=" + ComputeHmac(body, secret);
        return (body, sig == expected);
    }

    private static string ComputeHmac(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        return Convert.ToHexString(HMACSHA256.HashData(key, data)).ToLowerInvariant();
    }
}

public record CrmShipmentRequest(long CrmContractId, long? CustomerId, string? CustomerName, decimal Amount);
public record CrmPaymentRequest(long CrmContractId, long CrmPaymentId, decimal Amount, string? PaymentMethod, string? BankReference);
public record MesPurchaseRequest(long MesPurchaseOrderId, long? SupplierId, string? SupplierName, decimal Amount);
public record OaExpenseRequest(long OaApprovalId, decimal Amount, string? Summary);
