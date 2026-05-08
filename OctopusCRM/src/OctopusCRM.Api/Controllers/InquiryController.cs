using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusCRM.Api.Services;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[Route("api/inquiry")]
[Authorize]
public class InquiryController(InquiryService inquiryService) : ControllerBase
{
    private long GetCurrentUserId() =>
        long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] long? customerId, [FromQuery] string? status,
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var (rows, total) = await inquiryService.GetListAsync(customerId, status, keyword, page, size);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var inquiry = await inquiryService.GetByIdAsync(id);
        if (inquiry == null) return Ok(new { code = 404, msg = "询盘不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = inquiry });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInquiryRequest req)
    {
        var inquiry = await inquiryService.CreateAsync(
            req.CustomerId, req.Title, req.Description, req.ExpectedDelivery,
            req.AssignedTo, req.AssignedToName, GetCurrentUserId());
        return Ok(new { code = 200, msg = "创建成功", data = inquiry });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateInquiryRequest req)
    {
        var error = await inquiryService.UpdateAsync(
            id, req.Title, req.Description, req.ExpectedDelivery, req.AssignedTo, req.AssignedToName);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data = (object?)null });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var error = await inquiryService.DeleteAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "删除成功", data = (object?)null });
    }

    [HttpPost("{id:long}/item")]
    public async Task<IActionResult> AddItem(long id, [FromBody] CreateInquiryItemRequest req)
    {
        var item = await inquiryService.AddItemAsync(
            id, req.ProductName, req.Spec, req.Quantity, req.Unit, req.PlmProductId, req.Remark);
        return Ok(new { code = 200, msg = "添加成功", data = item });
    }

    [HttpPut("item/{itemId:long}")]
    public async Task<IActionResult> UpdateItem(long itemId, [FromBody] UpdateInquiryItemRequest req)
    {
        var error = await inquiryService.UpdateItemAsync(
            itemId, req.ProductName, req.Spec, req.Quantity, req.Unit, req.Remark);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data = (object?)null });
    }

    [HttpDelete("item/{itemId:long}")]
    public async Task<IActionResult> DeleteItem(long itemId)
    {
        var error = await inquiryService.DeleteItemAsync(itemId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "删除成功", data = (object?)null });
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateStatusRequest req)
    {
        var error = await inquiryService.UpdateStatusAsync(id, req.Status);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "状态更新成功", data = (object?)null });
    }
}

public record CreateInquiryRequest(
    long CustomerId,
    string Title,
    string? Description,
    DateTime? ExpectedDelivery,
    long AssignedTo,
    string? AssignedToName);

public record UpdateInquiryRequest(
    string? Title,
    string? Description,
    DateTime? ExpectedDelivery,
    long? AssignedTo,
    string? AssignedToName);

public record CreateInquiryItemRequest(
    string ProductName,
    string? Spec,
    decimal Quantity,
    string? Unit,
    long? PlmProductId,
    string? Remark);

public record UpdateInquiryItemRequest(
    string? ProductName,
    string? Spec,
    decimal? Quantity,
    string? Unit,
    string? Remark);

public record UpdateStatusRequest(string Status);
