using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/garment-team")]
public class GarmentTeamController(GarmentTeamService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] bool? includeInactive)
    {
        var data = await svc.ListAsync(keyword, status, includeInactive);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var t = await svc.GetAsync(id);
        if (t == null) return Ok(new { code = 404, msg = "组不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = t });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MesGarmentTeam req)
    {
        var (t, err) = await svc.CreateAsync(req);
        if (err != null) return Ok(new { code = 400, msg = err, data = (object?)null });
        return Ok(new { code = 200, msg = "创建成功", data = t });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] MesGarmentTeam req)
    {
        var (t, err) = await svc.UpdateAsync(req);
        if (t == null) return Ok(new { code = 404, msg = err, data = (object?)null });
        if (err != null) return Ok(new { code = 400, msg = err, data = t });
        return Ok(new { code = 200, msg = "更新成功", data = t });
    }

    public record SetStatusRequest(string Status);

    [HttpPut("{id}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] SetStatusRequest req)
    {
        var (t, err) = await svc.SetStatusAsync(id, req.Status);
        if (t == null) return Ok(new { code = 404, msg = err, data = (object?)null });
        if (err != null) return Ok(new { code = 400, msg = err, data = t });
        return Ok(new { code = 200, msg = "已切换", data = t });
    }

    [HttpGet("candidate-leaders")]
    public async Task<IActionResult> CandidateLeaders()
    {
        var data = await svc.GetCandidateLeadersAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }
}
