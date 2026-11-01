using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOMS.Api.Filters;
using OctopusOMS.Api.Services;

namespace OctopusOMS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/catalog")]
public class CatalogController(CatalogService svc) : ControllerBase
{
    [HttpGet("channels")]
    public async Task<IActionResult> Channels()
    {
        var data = await svc.GetChannelsAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("shops")]
    public async Task<IActionResult> Shops([FromQuery] long? channelId)
    {
        var data = await svc.GetShopsAsync(channelId);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("sku-mappings")]
    public async Task<IActionResult> Mappings([FromQuery] long? channelId)
    {
        var data = await svc.GetMappingsAsync(channelId);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost("sku-mappings")]
    [OperationLog("创建 SKU 映射")]
    public async Task<IActionResult> CreateMapping([FromBody] CreateMappingRequest req)
    {
        var (data, error) = await svc.CreateMappingAsync(req.ChannelId, req.ChannelSkuCode, req.PlmProductId, req.Note);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已创建", data });
    }
}

[Authorize]
[ApiController]
[Route("api/after-sale")]
public class AfterSaleController(AfterSaleService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var data = await svc.GetListAsync(status);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    [OperationLog("发起售后")]
    public async Task<IActionResult> Create([FromBody] CreateAfterSaleRequest req)
    {
        var (data, error) = await svc.CreateAsync(req.OrderId, req.Type, req.Amount, req.Reason ?? "", req.Note, UserId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已创建", data });
    }

    [HttpPut("{id:long}/approve")]
    [OperationLog("批准售后")]
    public async Task<IActionResult> Approve(long id, [FromBody] ApproveAfterSaleRequest req)
    {
        var (data, error) = await svc.ApproveAsync(id, req.Note);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已批准", data });
    }

    [HttpPut("{id:long}/complete")]
    [OperationLog("完成售后")]
    public async Task<IActionResult> Complete(long id)
    {
        var (data, error) = await svc.CompleteAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已完成", data });
    }
}

public record CreateMappingRequest(long ChannelId, string ChannelSkuCode, long PlmProductId, string? Note);
public record CreateAfterSaleRequest(long OrderId, string Type, decimal Amount, string? Reason, string? Note);
public record ApproveAfterSaleRequest(string? Note);
