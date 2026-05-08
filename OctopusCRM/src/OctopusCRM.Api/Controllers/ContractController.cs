using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;
using OctopusCRM.Api.Services;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[Route("api/contract")]
[Authorize]
public class ContractController(ContractService contractService, CrmDbContext db) : ControllerBase
{
    private long GetCurrentUserId() =>
        long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] long? customerId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var (rows, total) = await contractService.GetListAsync(customerId, status, page, size);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var contract = await contractService.GetByIdAsync(id);
        if (contract == null) return Ok(new { code = 404, msg = "合同不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = contract });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContractRequest req)
    {
        var (contract, error) = await contractService.CreateFromQuoteAsync(
            req.QuoteId, req.Title, req.DeliveryDate, req.SignDate,
            req.FileUrl, req.Remark, GetCurrentUserId());
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "创建成功", data = contract });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateContractRequest req)
    {
        var error = await contractService.UpdateAsync(id, req.Title, req.DeliveryDate, req.Remark);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data = (object?)null });
    }

    [HttpPut("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id)
    {
        var (approvalId, error) = await contractService.SubmitAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已提交审批", data = new { oaApprovalId = approvalId } });
    }

    [HttpPut("{id:long}/execute")]
    public async Task<IActionResult> Execute(long id)
    {
        var error = await contractService.ExecuteAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "合同已激活", data = (object?)null });
    }

    [HttpPut("{id:long}/ship")]
    public async Task<IActionResult> Ship(long id, [FromBody] ShipContractRequest req)
    {
        var error = await contractService.ShipAsync(id, req.TrackingNumber, req.ActualDeliveryDate);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "发货成功", data = (object?)null });
    }

    [HttpPut("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id)
    {
        var error = await contractService.CompleteAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "合同已完成", data = (object?)null });
    }

    [HttpPut("{id:long}/terminate")]
    public async Task<IActionResult> Terminate(long id, [FromBody] TerminateContractRequest req)
    {
        var error = await contractService.TerminateAsync(id, req.Reason);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "合同已终止", data = (object?)null });
    }

    [HttpPost("{id:long}/payment")]
    public async Task<IActionResult> AddPayment(long id, [FromBody] CreatePaymentRequest req)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return Ok(new { code = 404, msg = "合同不存在", data = (object?)null });

        var payment = new CrmPayment
        {
            ContractId = id,
            Amount = req.Amount,
            PaymentMethod = req.PaymentMethod,
            PaymentDate = req.PaymentDate,
            BankReference = req.BankReference,
            Remark = req.Remark,
            Status = "pending",
            CreatedBy = GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return Ok(new { code = 200, msg = "回款记录已创建", data = payment });
    }
}

[ApiController]
[Route("api/payment")]
[Authorize]
public class PaymentController(CrmDbContext db) : ControllerBase
{
    [HttpPut("{paymentId:long}/submit")]
    public async Task<IActionResult> Submit(long paymentId)
    {
        var payment = await db.Payments.FindAsync(paymentId);
        if (payment == null) return Ok(new { code = 404, msg = "回款记录不存在", data = (object?)null });
        if (payment.Status != "pending") return Ok(new { code = 400, msg = "仅待确认状态可提交审批", data = (object?)null });

        payment.Status = "pending_approval";
        payment.OaApprovalId = 99997L; // mock
        await db.SaveChangesAsync();
        return Ok(new { code = 200, msg = "已提交审批", data = new { oaApprovalId = payment.OaApprovalId } });
    }

    [HttpPut("{paymentId:long}/confirm")]
    public async Task<IActionResult> Confirm(long paymentId)
    {
        var payment = await db.Payments.FindAsync(paymentId);
        if (payment == null) return Ok(new { code = 404, msg = "回款记录不存在", data = (object?)null });
        if (payment.Status != "pending" && payment.Status != "pending_approval")
            return Ok(new { code = 400, msg = "无法确认当前状态的回款", data = (object?)null });

        payment.Status = "confirmed";
        await db.SaveChangesAsync();
        return Ok(new { code = 200, msg = "回款已确认", data = (object?)null });
    }
}

public record CreateContractRequest(
    long QuoteId,
    string Title,
    DateTime? DeliveryDate,
    DateTime? SignDate,
    string? FileUrl,
    string? Remark);

public record UpdateContractRequest(
    string? Title,
    DateTime? DeliveryDate,
    string? Remark);

public record ShipContractRequest(
    string? TrackingNumber,
    DateTime? ActualDeliveryDate);

public record TerminateContractRequest(string? Reason);

public record CreatePaymentRequest(
    decimal Amount,
    string? PaymentMethod,
    DateTime? PaymentDate,
    string? BankReference,
    string? Remark);
