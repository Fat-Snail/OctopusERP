using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;

namespace OctopusCRM.Api.Controllers;

/// <summary>商品维度的销售统计（供 PLM 列表页拉取 30 天销量等）</summary>
[ApiController]
[Route("api/contract")]
public class ProductStatsController(CrmDbContext db) : ControllerBase
{
    /// <summary>批量按 PlmProductId 查询销量（默认近 30 天，已签约合同的明细汇总）</summary>
    [HttpPost("sales-qty")]
    [AllowAnonymous]
    public async Task<IActionResult> SalesQty([FromBody] SalesQtyRequest req)
    {
        var ids = (req.ProductIds ?? Array.Empty<long>()).Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var days = req.Days is > 0 and <= 365 ? req.Days!.Value : 30;
        var since = DateTime.UtcNow.AddDays(-days);

        var rows = await db.Contracts
            .Where(c => c.Status != "draft" && c.Status != "cancelled" && c.CreatedAt >= since)
            .SelectMany(c => c.Items)
            .Where(it => it.PlmProductId.HasValue && ids.Contains(it.PlmProductId!.Value))
            .GroupBy(it => it.PlmProductId!.Value)
            .Select(g => new SalesQtyRow
            {
                PlmProductId = g.Key,
                SalesQty = g.Sum(x => x.Quantity),
                SalesAmount = g.Sum(x => x.Amount),
                ContractCount = g.Select(x => x.ContractId).Distinct().Count(),
                Days = days,
            })
            .ToListAsync();

        return Ok(new { code = 200, msg = "ok", data = rows });
    }
}

public record SalesQtyRequest(long[] ProductIds, int? Days);

public record SalesQtyRow
{
    public long PlmProductId { get; set; }
    public decimal SalesQty { get; set; }
    public decimal SalesAmount { get; set; }
    public int ContractCount { get; set; }
    public int Days { get; set; }
}
