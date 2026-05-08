using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusCRM.Api.Services;
using System.Security.Claims;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[Route("api/customer")]
[Authorize]
public class CustomerController(CustomerService customerService) : ControllerBase
{
    private long GetCurrentUserId() =>
        long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword, [FromQuery] string? level,
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var (rows, total) = await customerService.GetListAsync(keyword, level, status, page, size);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var customer = await customerService.GetByIdAsync(id);
        if (customer == null) return Ok(new { code = 404, msg = "客户不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = customer });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest req)
    {
        var customer = await customerService.CreateAsync(
            req.CustomerName, req.IndustryType, req.Level ?? "C",
            req.Status ?? "prospect", req.Address, req.Website, req.Remark, GetCurrentUserId());
        return Ok(new { code = 200, msg = "创建成功", data = customer });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCustomerRequest req)
    {
        var error = await customerService.UpdateAsync(
            id, req.CustomerName, req.IndustryType, req.Level, req.Status,
            req.Address, req.Website, req.Remark);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data = (object?)null });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var error = await customerService.DeleteAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "删除成功", data = (object?)null });
    }

    [HttpGet("{id:long}/contacts")]
    public async Task<IActionResult> GetContacts(long id)
    {
        var customer = await customerService.GetByIdAsync(id);
        if (customer == null) return Ok(new { code = 404, msg = "客户不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = customer.Contacts });
    }

    [HttpPost("{id:long}/contact")]
    public async Task<IActionResult> AddContact(long id, [FromBody] CreateContactRequest req)
    {
        var contact = await customerService.AddContactAsync(
            id, req.Name, req.Title, req.Phone, req.Email, req.IsPrimary, req.Remark);
        return Ok(new { code = 200, msg = "添加成功", data = contact });
    }

    [HttpPut("contact/{contactId:long}")]
    public async Task<IActionResult> UpdateContact(long contactId, [FromBody] UpdateContactRequest req)
    {
        var error = await customerService.UpdateContactAsync(
            contactId, req.Name, req.Title, req.Phone, req.Email, req.IsPrimary, req.Remark);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data = (object?)null });
    }

    [HttpDelete("contact/{contactId:long}")]
    public async Task<IActionResult> DeleteContact(long contactId)
    {
        var error = await customerService.DeleteContactAsync(contactId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "删除成功", data = (object?)null });
    }
}

public record CreateCustomerRequest(
    string CustomerName,
    string? IndustryType,
    string? Level,
    string? Status,
    string? Address,
    string? Website,
    string? Remark);

public record UpdateCustomerRequest(
    string? CustomerName,
    string? IndustryType,
    string? Level,
    string? Status,
    string? Address,
    string? Website,
    string? Remark);

public record CreateContactRequest(
    string Name,
    string? Title,
    string? Phone,
    string? Email,
    bool IsPrimary,
    string? Remark);

public record UpdateContactRequest(
    string? Name,
    string? Title,
    string? Phone,
    string? Email,
    bool? IsPrimary,
    string? Remark);
