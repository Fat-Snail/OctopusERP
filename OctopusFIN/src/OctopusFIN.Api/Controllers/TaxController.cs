using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Filters;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tax")]
public class TaxController(TaxService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    // ===== 发票 =====

    [HttpGet("invoices")]
    public async Task<IActionResult> ListInvoices(
        [FromQuery] string? status, [FromQuery] string? invoiceType,
        [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var (rows, total) = await svc.ListInvoicesAsync(status, invoiceType, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpGet("invoices/{id:long}")]
    public async Task<IActionResult> GetInvoice(long id)
    {
        var data = await svc.GetInvoiceAsync(id);
        if (data == null) return Ok(new { code = 404, msg = "发票不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost("invoices")]
    [OperationLog("开具电子发票")]
    public async Task<IActionResult> Issue([FromBody] InvoiceIssueInput req)
    {
        var (data, error) = await svc.IssueInvoiceAsync(req, UserId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "发票已开具", data });
    }

    [HttpPost("invoices/{invoiceNo}/verify")]
    public async Task<IActionResult> Verify(string invoiceNo)
    {
        var (valid, message) = await svc.VerifyInvoiceAsync(invoiceNo);
        return Ok(new { code = valid ? 200 : 400, msg = message, data = new { valid } });
    }

    [HttpPost("invoices/{invoiceNo}/red-flush")]
    [OperationLog("红冲发票")]
    public async Task<IActionResult> RedFlush(string invoiceNo)
    {
        var (success, error) = await svc.RedFlushAsync(invoiceNo);
        if (!success) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "红冲成功", data = (object?)null });
    }

    // ===== 纳税申报 =====

    [HttpGet("returns")]
    public async Task<IActionResult> ListReturns(
        [FromQuery] string? taxType, [FromQuery] string? status,
        [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var (rows, total) = await svc.ListTaxReturnsAsync(taxType, status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpPost("returns")]
    [OperationLog("提交纳税申报")]
    public async Task<IActionResult> File([FromBody] TaxFilingInput req)
    {
        var (data, error) = await svc.FileTaxReturnAsync(req);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "申报已提交", data });
    }

    // ===== 诊断 =====

    [HttpGet("adapter")]
    public IActionResult AdapterInfo()
    {
        var (code, isMock) = svc.GetAdapterInfo();
        return Ok(new { code = 200, msg = "ok", data = new { adapterCode = code, isMock } });
    }
}
