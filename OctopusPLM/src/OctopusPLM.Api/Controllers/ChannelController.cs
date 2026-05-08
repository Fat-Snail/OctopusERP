using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.Services;

namespace OctopusPLM.Api.Controllers;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class CreateChannelRequest
{
    public string ChannelCode { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
}

public class UpdateChannelRequest
{
    public string ChannelName { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
}

public class CreateChannelCategoryMappingRequest
{
    public long CategoryId { get; set; }
    public string ExternalCategoryId { get; set; } = string.Empty;
    public string ExternalCategoryName { get; set; } = string.Empty;
    public string MappingVersion { get; set; } = "v1";
}

public class UpdateChannelCategoryMappingRequest
{
    public string ExternalCategoryId { get; set; } = string.Empty;
    public string ExternalCategoryName { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
}

public class CreateChannelAttributeMappingRequest
{
    public long AttributeId { get; set; }
    public string ExternalAttributeId { get; set; } = string.Empty;
    public string ExternalAttributeName { get; set; } = string.Empty;
    public bool IsRequiredOutbound { get; set; }
    public string? TransformRuleJson { get; set; }
}

public class UpdateChannelAttributeMappingRequest
{
    public string ExternalAttributeId { get; set; } = string.Empty;
    public string ExternalAttributeName { get; set; } = string.Empty;
    public bool IsRequiredOutbound { get; set; }
    public string? TransformRuleJson { get; set; }
}

// ── ChannelController ─────────────────────────────────────────────────────────

/// <summary>渠道映射管理</summary>
[ApiController]
[Route("api/channel")]
[Authorize]
public class ChannelController : ControllerBase
{
    private readonly ChannelService _service;

    public ChannelController(ChannelService service) => _service = service;

    /// <summary>渠道列表</summary>
    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var channels = await _service.GetAllChannelsAsync();
        return Ok(new { code = 200, msg = "ok", data = channels });
    }

    /// <summary>创建渠道</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChannelRequest req)
    {
        var (channel, err) = await _service.CreateChannelAsync(req.ChannelCode, req.ChannelName);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "创建成功", data = new { channel!.ChannelId } });
    }

    /// <summary>更新渠道</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateChannelRequest req)
    {
        var err = await _service.UpdateChannelAsync(id, req.ChannelName, req.Status);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "更新成功" });
    }

    /// <summary>删除渠道（无映射时才允许）</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var err = await _service.DeleteChannelAsync(id);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>渠道类目映射列表（含属性映射）</summary>
    [HttpGet("{id:long}/mappings")]
    public async Task<IActionResult> GetMappings(long id)
    {
        var mappings = await _service.GetCategoryMappingsAsync(id);
        return Ok(new { code = 200, msg = "ok", data = mappings });
    }

    /// <summary>创建类目映射</summary>
    [HttpPost("{id:long}/mappings")]
    public async Task<IActionResult> CreateMapping(long id, [FromBody] CreateChannelCategoryMappingRequest req)
    {
        var (mapping, err) = await _service.CreateCategoryMappingAsync(id, req.CategoryId, req.ExternalCategoryId, req.ExternalCategoryName, req.MappingVersion);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "创建成功", data = new { mapping!.MappingId } });
    }

    /// <summary>更新类目映射</summary>
    [HttpPut("{id:long}/mappings/{mappingId:long}")]
    public async Task<IActionResult> UpdateMapping(long id, long mappingId, [FromBody] UpdateChannelCategoryMappingRequest req)
    {
        var err = await _service.UpdateCategoryMappingAsync(id, mappingId, req.ExternalCategoryId, req.ExternalCategoryName, req.Status);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "更新成功" });
    }

    /// <summary>删除类目映射（含其所有属性映射）</summary>
    [HttpDelete("{id:long}/mappings/{mappingId:long}")]
    public async Task<IActionResult> DeleteMapping(long id, long mappingId)
    {
        var err = await _service.DeleteCategoryMappingAsync(id, mappingId);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>添加属性映射</summary>
    [HttpPost("{id:long}/mappings/{mappingId:long}/attributes")]
    public async Task<IActionResult> CreateAttributeMapping(long id, long mappingId, [FromBody] CreateChannelAttributeMappingRequest req)
    {
        var (attrMapping, err) = await _service.CreateAttributeMappingAsync(
            id, mappingId, req.AttributeId, req.ExternalAttributeId, req.ExternalAttributeName, req.IsRequiredOutbound, req.TransformRuleJson);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "添加成功", data = new { attrMapping!.MappingId } });
    }

    /// <summary>更新属性映射</summary>
    [HttpPut("{id:long}/mappings/{mappingId:long}/attributes/{attrMappingId:long}")]
    public async Task<IActionResult> UpdateAttributeMapping(long id, long mappingId, long attrMappingId, [FromBody] UpdateChannelAttributeMappingRequest req)
    {
        var err = await _service.UpdateAttributeMappingAsync(
            id, mappingId, attrMappingId, req.ExternalAttributeId, req.ExternalAttributeName, req.IsRequiredOutbound, req.TransformRuleJson);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "更新成功" });
    }

    /// <summary>删除属性映射</summary>
    [HttpDelete("{id:long}/mappings/{mappingId:long}/attributes/{attrMappingId:long}")]
    public async Task<IActionResult> DeleteAttributeMapping(long id, long mappingId, long attrMappingId)
    {
        var err = await _service.DeleteAttributeMappingAsync(id, mappingId, attrMappingId);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "删除成功" });
    }
}
