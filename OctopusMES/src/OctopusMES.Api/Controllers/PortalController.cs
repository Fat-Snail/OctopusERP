using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

/// <summary>
/// P16 供应商门户专属端点。经网关 /gw/mes/api/portal/** 转发。
/// 所有端点以"仅本人"为原则：通过 PortalIdentityService 解析 sub→供应商。
/// 响应 data 结构直接对齐门户 mock 契约（前端 unwrap 后无需二次映射）。
/// </summary>
[ApiController]
[Route("api/portal")]
[Authorize]
public class PortalController(
    MesDbContext db,
    PortalIdentityService portalIdentity) : ControllerBase
{
    private long CurrentUserId =>
        long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    /// <summary>当前门户身份：绑定供应商 id/名称；未绑定返回 bound=false。</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var supplierId = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!supplierId.HasValue)
            return Ok(new { code = 200, msg = "ok", data = new { bound = false, supplierId = (long?)null, supplierName = (string?)null } });

        var name = await db.Suppliers.Where(s => s.SupplierId == supplierId.Value)
            .Select(s => s.SupplierName).FirstOrDefaultAsync();
        return Ok(new { code = 200, msg = "ok", data = new { bound = true, supplierId, supplierName = name } });
    }

    /// <summary>门户汇总：从本人采购单/报价聚合。</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 403, msg = "非供应商门户身份", data = (object?)null });

        var pendingQuotes = await db.SupplierQuotes.CountAsync(q => q.SupplierId == sid && q.Status == "pending");
        var activeOrders = await db.PurchaseOrders.CountAsync(o => o.SupplierId == sid && o.Status != "completed");
        var readyToShip = await db.PurchaseOrders.CountAsync(o => o.SupplierId == sid && (o.Status == "approved" || o.Status == "confirmed"));
        var outstanding = await db.PurchaseOrders
            .Where(o => o.SupplierId == sid && o.Status != "completed")
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        return Ok(new { code = 200, msg = "ok", data = new { pendingQuotes, activeOrders, readyToShip, outstandingAmount = outstanding } });
    }

    /// <summary>询价响应列表：仅本人收到的询价。</summary>
    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuoteRequests()
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var rows = await db.SupplierQuotes.Where(q => q.SupplierId == sid).OrderByDescending(q => q.Deadline).ToListAsync();
        var result = rows.Select(q => new
        {
            id = q.QuoteId.ToString(),
            code = q.Code,
            productName = q.ProductName,
            sku = q.Sku,
            quantity = q.Quantity,
            unit = q.Unit,
            deadline = q.Deadline.ToString("yyyy-MM-dd HH:mm"),
            status = q.Status,
            myQuote = q.UnitPrice.HasValue ? new
            {
                unitPrice = q.UnitPrice.Value,
                deliveryDays = q.DeliveryDays ?? 0,
                remark = q.QuoteRemark,
                quotedAt = q.QuotedAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
                status = q.QuoteStatus ?? "under_review",
            } : null,
        }).ToList();

        return Ok(new { code = 200, msg = "ok", data = result });
    }

    /// <summary>在线报价：仅能响应本人收到的询价。</summary>
    [HttpPost("quotes/{id:long}/submit")]
    public async Task<IActionResult> SubmitQuote(long id, [FromBody] SubmitQuoteRequest req)
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 403, msg = "非供应商门户身份", data = (object?)null });

        var quote = await db.SupplierQuotes.FirstOrDefaultAsync(q => q.QuoteId == id && q.SupplierId == sid.Value);
        if (quote == null) return Ok(new { code = 404, msg = "询价不存在", data = (object?)null });

        quote.UnitPrice = req.UnitPrice;
        quote.DeliveryDays = req.DeliveryDays;
        quote.QuoteRemark = req.Remark;
        quote.QuotedAt = DateTime.UtcNow;
        quote.QuoteStatus = "under_review";
        quote.Status = "responded";
        await db.SaveChangesAsync();

        return Ok(new
        {
            code = 200, msg = "ok",
            data = new
            {
                id = quote.QuoteId.ToString(), code = quote.Code, productName = quote.ProductName,
                sku = quote.Sku, quantity = quote.Quantity, unit = quote.Unit,
                deadline = quote.Deadline.ToString("yyyy-MM-dd HH:mm"), status = quote.Status,
                myQuote = new
                {
                    unitPrice = quote.UnitPrice, deliveryDays = quote.DeliveryDays ?? 0,
                    remark = quote.QuoteRemark, quotedAt = quote.QuotedAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
                    status = quote.QuoteStatus,
                },
            },
        });
    }

    /// <summary>发货登记列表：本人登记的运单。</summary>
    [HttpGet("shipments")]
    public async Task<IActionResult> GetShipments()
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var rows = await db.SupplierShipments.Where(s => s.SupplierId == sid).OrderByDescending(s => s.ShippedAt).ToListAsync();
        var result = rows.Select(s => new
        {
            id = s.ShipmentId.ToString(), orderCode = s.OrderCode, trackingNo = s.TrackingNo,
            carrier = s.Carrier, shippedAt = s.ShippedAt.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();

        return Ok(new { code = 200, msg = "ok", data = result });
    }

    /// <summary>登记发货：仅能对本人采购单登记。</summary>
    [HttpPost("shipments")]
    public async Task<IActionResult> RegisterShipment([FromBody] RegisterShipmentRequest req)
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 403, msg = "非供应商门户身份", data = (object?)null });

        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.PurchaseId == req.OrderId && o.SupplierId == sid.Value);
        if (order == null) return Ok(new { code = 404, msg = "采购单不存在", data = (object?)null });

        var shipment = new MesSupplierShipment
        {
            SupplierId = sid.Value, PurchaseId = order.PurchaseId, OrderCode = order.PurchaseCode,
            TrackingNo = req.TrackingNo ?? string.Empty, Carrier = req.Carrier ?? string.Empty,
            ShippedAt = DateTime.UtcNow,
        };
        db.SupplierShipments.Add(shipment);
        await db.SaveChangesAsync();

        return Ok(new
        {
            code = 200, msg = "ok",
            data = new { orderCode = shipment.OrderCode, trackingNo = shipment.TrackingNo, carrier = shipment.Carrier, shippedAt = shipment.ShippedAt.ToString("yyyy-MM-dd HH:mm") },
        });
    }

    /// <summary>对账汇总：本人采购单的应付/已结/未结（按状态推算）。</summary>
    [HttpGet("statement/summary")]
    public async Task<IActionResult> GetStatementSummary()
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 403, msg = "非供应商门户身份", data = (object?)null });

        var orders = await db.PurchaseOrders.Where(o => o.SupplierId == sid).ToListAsync();
        var payable = orders.Sum(o => o.TotalAmount);
        var settled = orders.Where(o => o.Status == "completed" || o.Status == "shipped").Sum(o => o.TotalAmount);
        return Ok(new { code = 200, msg = "ok", data = new { payable, settled, outstanding = payable - settled } });
    }

    /// <summary>对账明细：本人采购单流水。</summary>
    [HttpGet("statement/items")]
    public async Task<IActionResult> GetStatementItems()
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var orders = await db.PurchaseOrders.Where(o => o.SupplierId == sid).OrderByDescending(o => o.CreatedAt).ToListAsync();
        var items = orders.Select(o => new
        {
            id = o.PurchaseId.ToString(),
            date = o.CreatedAt.ToString("yyyy-MM-dd"),
            code = o.PurchaseCode,
            description = $"采购订单 {o.PurchaseCode} 货款",
            amount = o.TotalAmount,
            status = o.Status == "completed" || o.Status == "shipped" ? "settled" : "pending",
        }).ToList();

        return Ok(new { code = 200, msg = "ok", data = items });
    }

    /// <summary>资质档案列表：仅本人。</summary>
    [HttpGet("qualifications")]
    public async Task<IActionResult> GetQualifications()
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var rows = await db.SupplierQualifications.Where(q => q.SupplierId == sid).OrderByDescending(q => q.UploadedAt).ToListAsync();
        var result = rows.Select(q => new
        {
            id = q.QualificationId.ToString(), title = q.Title, category = q.Category,
            fileName = q.FileName, fileSize = q.FileSize, status = q.Status,
            uploadedAt = q.UploadedAt.ToString("yyyy-MM-dd"),
            validUntil = q.ValidUntil?.ToString("yyyy-MM-dd"),
        }).ToList();

        return Ok(new { code = 200, msg = "ok", data = result });
    }

    /// <summary>上传资质：归属本人供应商。</summary>
    [HttpPost("qualifications")]
    public async Task<IActionResult> UploadQualification([FromBody] UploadQualificationRequest req)
    {
        var sid = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!sid.HasValue) return Ok(new { code = 403, msg = "非供应商门户身份", data = (object?)null });

        var qual = new MesSupplierQualification
        {
            SupplierId = sid.Value, Title = req.Title ?? string.Empty, Category = req.Category ?? string.Empty,
            FileName = req.FileName ?? string.Empty, FileSize = req.FileSize ?? "—",
            Status = "pending", UploadedAt = DateTime.UtcNow,
        };
        db.SupplierQualifications.Add(qual);
        await db.SaveChangesAsync();

        return Ok(new
        {
            code = 200, msg = "ok",
            data = new
            {
                id = qual.QualificationId.ToString(), title = qual.Title, category = qual.Category,
                fileName = qual.FileName, fileSize = qual.FileSize, status = qual.Status,
                uploadedAt = qual.UploadedAt.ToString("yyyy-MM-dd"), validUntil = (string?)null,
            },
        });
    }
}

public record SubmitQuoteRequest(decimal UnitPrice, int DeliveryDays, string? Remark);
public record RegisterShipmentRequest(long OrderId, string? TrackingNo, string? Carrier);
public record UploadQualificationRequest(string? Title, string? Category, string? FileName, string? FileSize);
