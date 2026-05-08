using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusCRM.Api.Services;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[Route("api/quote")]
[Authorize]
public class QuoteController(QuoteService quoteService) : ControllerBase
{
    private long GetCurrentUserId() =>
        long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] long? customerId, [FromQuery] long? inquiryId,
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var (rows, total) = await quoteService.GetListAsync(customerId, inquiryId, status, page, size);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var quote = await quoteService.GetByIdAsync(id);
        if (quote == null) return Ok(new { code = 404, msg = "报价单不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = quote });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuoteRequest req)
    {
        var quote = await quoteService.CreateAsync(
            req.InquiryId, req.CustomerId, req.Currency, req.ValidUntil,
            req.ExpectedDelivery, req.Terms, req.Remark, GetCurrentUserId());
        return Ok(new { code = 200, msg = "创建成功", data = quote });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateQuoteRequest req)
    {
        var error = await quoteService.UpdateAsync(
            id, req.Currency, req.ValidUntil, req.ExpectedDelivery, req.Terms, req.Remark);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data = (object?)null });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var error = await quoteService.DeleteAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "删除成功", data = (object?)null });
    }

    [HttpPost("{id:long}/item")]
    public async Task<IActionResult> AddItem(long id, [FromBody] CreateQuoteItemRequest req)
    {
        var item = await quoteService.AddItemAsync(
            id, req.ProductName, req.Quantity, req.UnitPrice,
            req.Spec, req.Unit, req.PlmProductId, req.Remark);
        return Ok(new { code = 200, msg = "添加成功", data = item });
    }

    [HttpPut("item/{itemId:long}")]
    public async Task<IActionResult> UpdateItem(long itemId, [FromBody] UpdateQuoteItemRequest req)
    {
        var error = await quoteService.UpdateItemAsync(
            itemId, req.ProductName, req.Quantity, req.UnitPrice, req.Spec, req.Unit, req.Remark);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data = (object?)null });
    }

    [HttpDelete("item/{itemId:long}")]
    public async Task<IActionResult> DeleteItem(long itemId)
    {
        var error = await quoteService.DeleteItemAsync(itemId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "删除成功", data = (object?)null });
    }

    [HttpPut("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id)
    {
        var (approvalId, error) = await quoteService.SubmitAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已提交审批", data = new { oaApprovalId = approvalId } });
    }

    [HttpPut("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id)
    {
        var error = await quoteService.ConfirmAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "报价已确认", data = (object?)null });
    }
}

public record CreateQuoteRequest(
    long InquiryId,
    long CustomerId,
    string? Currency,
    DateTime? ValidUntil,
    DateTime? ExpectedDelivery,
    string? Terms,
    string? Remark);

public record UpdateQuoteRequest(
    string? Currency,
    DateTime? ValidUntil,
    DateTime? ExpectedDelivery,
    string? Terms,
    string? Remark);

public record CreateQuoteItemRequest(
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    string? Spec,
    string? Unit,
    long? PlmProductId,
    string? Remark);

public record UpdateQuoteItemRequest(
    string? ProductName,
    decimal? Quantity,
    decimal? UnitPrice,
    string? Spec,
    string? Unit,
    string? Remark);
