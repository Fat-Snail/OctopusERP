using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace OctopusWMS.Api.Controllers;

[ApiController]
[Route("api/webhook")]
[AllowAnonymous]
public class WebhookController(WmsDbContext db, IConfiguration config) : ControllerBase
{
    private const string WmsSignatureHeader = "X-Wms-Signature";

    [HttpPost("reserve-stock")]
    public async Task<IActionResult> ReserveStock([FromBody] ReserveStockRequest req)
    {
        if (!VerifySignature(config["WmsSync:SharedSecret"] ?? "wms-shared-secret-dev", out var err))
            return Unauthorized(new { code = 401, msg = err });

        foreach (var item in req.Items)
        {
            var inv = await db.Inventories.FirstOrDefaultAsync(x =>
                (item.PlmProductId.HasValue && x.PlmProductId == item.PlmProductId) ||
                x.ProductName == item.ProductName);

            if (inv is not null)
            {
                inv.LockedQuantity += item.Quantity;
                inv.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return Ok(new { code = 200, msg = "ok", data = (object?)null });
    }

    [HttpPost("receive-goods")]
    public async Task<IActionResult> ReceiveGoods([FromBody] ReceiveGoodsRequest req)
    {
        if (!VerifySignature(config["WmsSync:SharedSecret"] ?? "wms-shared-secret-dev", out var err))
            return Unauthorized(new { code = 401, msg = err });

        var warehouse = await db.Warehouses.OrderBy(x => x.WarehouseId).FirstOrDefaultAsync();
        if (warehouse is null)
            return Ok(new { code = 200, msg = "no warehouse, skipped", data = (object?)null });

        var inbound = new WmsInboundOrder
        {
            InboundCode = $"IN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            InboundType = "production",
            WarehouseId = warehouse.WarehouseId,
            MesWorkOrderId = req.MesWorkOrderId,
            Status = "completed",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };
        db.InboundOrders.Add(inbound);
        await db.SaveChangesAsync();

        db.InboundItems.Add(new WmsInboundItem
        {
            InboundId = inbound.InboundId,
            PlmProductId = req.PlmProductId,
            ProductName = req.ProductName,
            Spec = req.Spec,
            Unit = req.Unit,
            ExpectedQty = req.Quantity,
            ReceivedQty = req.Quantity
        });

        var inv = await db.Inventories.FirstOrDefaultAsync(x =>
            x.WarehouseId == warehouse.WarehouseId &&
            ((req.PlmProductId.HasValue && x.PlmProductId == req.PlmProductId) ||
              x.ProductName == req.ProductName));

        if (inv is null)
        {
            db.Inventories.Add(new WmsInventory
            {
                WarehouseId = warehouse.WarehouseId,
                PlmProductId = req.PlmProductId,
                ProductName = req.ProductName,
                Spec = req.Spec,
                Unit = req.Unit,
                Quantity = req.Quantity,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            inv.Quantity += req.Quantity;
            inv.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { code = 200, msg = "ok", data = (object?)null });
    }

    private bool VerifySignature(string secret, out string error)
    {
        error = string.Empty;
        if (!Request.Headers.TryGetValue(WmsSignatureHeader, out var sig))
        {
            error = "缺少签名";
            return false;
        }

        Request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = reader.ReadToEndAsync().GetAwaiter().GetResult();

        var expected = "sha256=" + ComputeHmac(body, secret);
        if (!string.Equals(sig.ToString(), expected, StringComparison.OrdinalIgnoreCase))
        {
            error = "签名验证失败";
            return false;
        }
        return true;
    }

    private static string ComputeHmac(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();
    }
}

public record ReserveStockRequest(long CrmContractId, ReserveStockItem[] Items);
public record ReserveStockItem(string ProductName, long? PlmProductId, decimal Quantity);
public record ReceiveGoodsRequest(
    long MesWorkOrderId,
    long? PlmProductId,
    string ProductName,
    string? Spec,
    string? Unit,
    decimal Quantity);
