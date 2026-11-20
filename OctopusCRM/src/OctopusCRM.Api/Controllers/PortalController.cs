using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;
using OctopusCRM.Api.Services;

namespace OctopusCRM.Api.Controllers;

/// <summary>
/// P16 客户门户专属端点。经网关 /gw/crm/api/portal/** 转发。
/// 所有端点以"仅本人"为原则：通过 PortalIdentityService 解析 sub→客户，
/// 未绑定身份返回 bound=false 或空数据，绝不返回其它客户的数据。
/// 响应 data 结构直接对齐门户 mock 契约（前端 unwrap 后无需二次映射）。
/// </summary>
[ApiController]
[Route("api/portal")]
[Authorize]
public class PortalController(
    CrmDbContext db,
    PortalIdentityService portalIdentity) : ControllerBase
{
    private long CurrentUserId =>
        long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    private static bool IsPaid(string? status) =>
        status is "paid" or "confirmed" or "settled";

    /// <summary>当前门户身份：绑定客户 id/名称；未绑定返回 bound=false。</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var customerId = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        if (!customerId.HasValue)
            return Ok(new { code = 200, msg = "ok", data = new { bound = false, customerId = (long?)null, customerName = (string?)null } });

        var name = await db.Customers.Where(c => c.CustomerId == customerId.Value)
            .Select(c => c.CustomerName).FirstOrDefaultAsync();
        return Ok(new { code = 200, msg = "ok", data = new { bound = true, customerId, customerName = name } });
    }

    /// <summary>门户汇总：从本人真实询盘/合同/回款聚合。</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var cid = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        if (!cid.HasValue) return Ok(new { code = 403, msg = "非客户门户身份", data = (object?)null });

        var openInquiries = await db.Inquiries.CountAsync(i => i.CustomerId == cid && i.Status == "open");
        var activeContracts = await db.Contracts.CountAsync(c => c.CustomerId == cid && c.Status == "active");
        var shippingOrders = await db.Contracts.CountAsync(c => c.CustomerId == cid && (c.Status == "shipped" || c.Status == "completed"));

        var contractIds = await db.Contracts.Where(c => c.CustomerId == cid).Select(c => c.ContractId).ToListAsync();
        var unpaidAmount = await db.Payments
            .Where(p => contractIds.Contains(p.ContractId) && p.Status != "paid" && p.Status != "confirmed" && p.Status != "settled")
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return Ok(new { code = 200, msg = "ok", data = new { openInquiries, activeContracts, shippingOrders, unpaidAmount } });
    }

    /// <summary>物流追踪：基于本人已发货/已完成合同生成轨迹（真实运单号）。</summary>
    [HttpGet("shipments")]
    public async Task<IActionResult> GetShipments()
    {
        var cid = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        if (!cid.HasValue) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var contracts = await db.Contracts
            .Where(c => c.CustomerId == cid && (c.Status == "shipped" || c.Status == "completed"))
            .OrderByDescending(c => c.ActualDeliveryDate).ToListAsync();

        var result = contracts.Select((c, idx) =>
        {
            var traces = new List<object>();
            if (c.Status == "completed")
                traces.Add(new { time = c.ActualDeliveryDate?.ToString("MM-dd HH:mm") ?? "", desc = "快件已签收", location = "" });
            traces.Add(new { time = c.ActualDeliveryDate?.ToString("MM-dd HH:mm") ?? "", desc = "仓库已发货，快件交接完成", location = "" });
            return new
            {
                id = (idx + 1).ToString(),
                trackingNo = c.TrackingNumber ?? "—",
                carrier = c.Carrier ?? "—",
                status = c.Status == "completed" ? "delivered" : "shipping",
                contractCode = c.ContractCode,
                productName = c.Title,
                traces,
            };
        }).ToList();

        return Ok(new { code = 200, msg = "ok", data = result });
    }

    /// <summary>对账汇总：本人合同的应收/已收/未收（真实回款记录）。</summary>
    [HttpGet("statement/summary")]
    public async Task<IActionResult> GetStatementSummary()
    {
        var cid = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        if (!cid.HasValue) return Ok(new { code = 403, msg = "非客户门户身份", data = (object?)null });

        var contracts = await db.Contracts.Where(c => c.CustomerId == cid).ToListAsync();
        var receivable = contracts.Sum(c => c.TotalAmount);
        var ids = contracts.Select(c => c.ContractId).ToList();
        var paid = await db.Payments
            .Where(p => ids.Contains(p.ContractId) && (p.Status == "paid" || p.Status == "confirmed"))
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return Ok(new { code = 200, msg = "ok", data = new { receivable, paid, unpaid = receivable - paid } });
    }

    /// <summary>对账明细：本人合同的回款/发票流水。</summary>
    [HttpGet("statement/items")]
    public async Task<IActionResult> GetStatementItems()
    {
        var cid = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        if (!cid.HasValue) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        // 先拉实体再投影：日期格式化/字符串插值在 EF SQLite 下无法翻译为 SQL
        var payments = await db.Payments
            .Include(p => p.Contract)
            .Where(p => p.Contract!.CustomerId == cid)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        var items = payments.Select(p => new
        {
            id = p.PaymentId.ToString(),
            date = (p.PaymentDate ?? p.CreatedAt).ToString("yyyy-MM-dd"),
            type = IsPaid(p.Status) ? "payment" : "invoice",
            code = p.BankReference ?? $"PAY{p.PaymentId}",
            description = $"合同 {p.Contract!.ContractCode} 回款",
            amount = p.Amount,
            status = IsPaid(p.Status) ? "settled" : "pending",
        }).ToList();

        return Ok(new { code = 200, msg = "ok", data = items });
    }

    /// <summary>在线客服历史：仅本人消息。</summary>
    [HttpGet("chat")]
    public async Task<IActionResult> GetChatHistory()
    {
        var cid = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        if (!cid.HasValue) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var rows = await db.PortalChatMessages
            .Where(m => m.CustomerId == cid)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        var msgs = rows.Select(m => new
        {
            id = m.Id.ToString(),
            from = m.From,
            text = m.Text,
            time = m.CreatedAt.ToString("HH:mm"),
        }).ToList();

        return Ok(new { code = 200, msg = "ok", data = msgs });
    }

    /// <summary>发送客服消息：归属本人客户。</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> SendChatMessage([FromBody] PortalChatRequest req)
    {
        var cid = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        if (!cid.HasValue) return Ok(new { code = 403, msg = "非客户门户身份", data = (object?)null });

        var msg = new CrmPortalChatMessage
        {
            CustomerId = cid.Value, From = "me",
            Text = req.Text ?? string.Empty, CreatedAt = DateTime.UtcNow,
        };
        db.PortalChatMessages.Add(msg);
        await db.SaveChangesAsync();

        return Ok(new
        {
            code = 200, msg = "ok",
            data = new { id = msg.Id.ToString(), from = "me", text = msg.Text, time = msg.CreatedAt.ToString("HH:mm") },
        });
    }

    /// <summary>可下载文档：全局文档 + 本人专属文档。</summary>
    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments()
    {
        var cid = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        var rows = await db.PortalDocuments
            .Where(d => d.IsGlobal || (cid.HasValue && d.CustomerId == cid))
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();
        var docs = rows.Select(d => new
        {
            id = d.Id.ToString(),
            title = d.Title,
            category = d.Category,
            size = d.FileSize,
            updatedAt = d.UpdatedAt.ToString("yyyy-MM-dd"),
            downloads = d.Downloads,
        }).ToList();

        return Ok(new { code = 200, msg = "ok", data = docs });
    }
}

public record PortalChatRequest(string? Text);
