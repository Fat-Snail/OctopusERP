using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[ApiController]
[Route("api/webhook")]
[AllowAnonymous]
public class WebhookController(MesDbContext db, IConfiguration config, GarmentScheduleService garmentSvc) : ControllerBase
{
    private const string MesSignatureHeader = "X-Mes-Signature";

    /// <summary>
    /// CRM 合同激活 → 创建/更新 MES 服装订单池条目（待生产经理拆分到组×日）
    /// </summary>
    [HttpPost("garment-order-created")]
    public async Task<IActionResult> GarmentOrderCreated([FromBody] GarmentOrderCreatedRequest req)
    {
        var secret = config["MesSync:SharedSecret"] ?? "mes-shared-secret-dev";
        if (!VerifySignature(secret, out var err))
            return Unauthorized(new { code = 401, msg = err });

        // 同合同幂等：以 ContractId 唯一索引为准
        var existing = await db.GarmentOrders.FirstOrDefaultAsync(x => x.ContractId == req.ContractId);
        var now = DateTime.UtcNow;

        if (existing != null)
        {
            existing.CustomerId = req.CustomerId;
            existing.CustomerName = req.CustomerName;
            existing.OrderNo = req.OrderNo;
            existing.PlmProductId = req.PlmProductId;
            existing.StyleCode = req.StyleCode;
            existing.Quantity = req.Quantity;
            existing.Season = req.Season;
            existing.DueDate = req.DueDate;
            existing.IsAdditional = req.IsAdditional;
            existing.OriginalContractId = req.OriginalContractId;
            existing.UpdatedAt = now;
            await db.SaveChangesAsync();
            return Ok(new { code = 200, msg = "updated", data = new { existing.OrderId } });
        }

        var order = new MesGarmentOrder
        {
            ContractId = req.ContractId,
            CustomerId = req.CustomerId,
            CustomerName = req.CustomerName,
            OrderNo = req.OrderNo,
            PlmProductId = req.PlmProductId,
            StyleCode = req.StyleCode,
            Quantity = req.Quantity,
            Season = req.Season,
            DueDate = req.DueDate,
            IsAdditional = req.IsAdditional,
            OriginalContractId = req.OriginalContractId,
            Status = "pending_schedule",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.GarmentOrders.Add(order);
        await db.SaveChangesAsync();
        return Ok(new { code = 200, msg = "created", data = new { order.OrderId } });
    }

    /// <summary>
    /// WMS 回布完成 → 更新 MES GarmentSchedule.FabricStatus
    /// </summary>
    [HttpPost("fabric-arrived")]
    public async Task<IActionResult> FabricArrived([FromBody] FabricArrivedRequest req)
    {
        var secret = config["MesSync:SharedSecret"] ?? "mes-shared-secret-dev";
        if (!VerifySignature(secret, out var err))
            return Unauthorized(new { code = 401, msg = err });

        // 优先按 ScheduleId 更新
        if (req.MesScheduleId.HasValue)
        {
            var s = await db.GarmentSchedules.FindAsync(req.MesScheduleId.Value);
            if (s != null)
            {
                s.FabricStatus = req.FabricStatus;
                s.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Ok(new { code = 200, msg = "schedule updated", data = (object?)null });
            }
        }
        // 否则按 OrderId 批量更新该订单下所有排程
        if (req.MesOrderId.HasValue)
        {
            var rows = await db.GarmentSchedules.Where(x => x.OrderId == req.MesOrderId.Value).ToListAsync();
            foreach (var s in rows)
            {
                s.FabricStatus = req.FabricStatus;
                s.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            return Ok(new { code = 200, msg = $"{rows.Count} schedules updated", data = (object?)null });
        }
        return Ok(new { code = 200, msg = "no target, skipped", data = (object?)null });
    }

    /// <summary>
    /// OA 审批回调：approved=true → 创建 MES 工单 + GarmentSchedule.WorkOrderId 回填
    /// </summary>
    [HttpPost("approval-callback")]
    public async Task<IActionResult> ApprovalCallback([FromBody] ApprovalCallbackRequest req)
    {
        var secret = config["MesSync:SharedSecret"] ?? "mes-shared-secret-dev";
        if (!VerifySignature(secret, out var err))
            return Unauthorized(new { code = 401, msg = err });

        var (schedule, wo, error) = await garmentSvc.HandleApprovalCallbackAsync(req.OaApprovalId, req.Approved);
        if (schedule == null)
            return Ok(new { code = 404, msg = error, data = (object?)null });
        if (error != null)
            return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = new
        {
            schedule.ScheduleId,
            schedule.ScheduleStatus,
            workOrderId = wo?.WorkOrderId,
            workOrderCode = wo?.WorkOrderCode,
        }});
    }

    private bool VerifySignature(string secret, out string error)
    {
        error = string.Empty;
        if (!Request.Headers.TryGetValue(MesSignatureHeader, out var sig))
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

public record GarmentOrderCreatedRequest(
    long ContractId,
    long CustomerId,
    string CustomerName,
    string OrderNo,
    long? PlmProductId,
    string? StyleCode,
    decimal Quantity,
    string? Season,
    DateTime? DueDate,
    bool IsAdditional,
    long? OriginalContractId);

public record FabricArrivedRequest(
    long? MesScheduleId,
    long? MesOrderId,
    string ArrivalCode,
    string FabricStatus,
    bool IsComplete,
    DateTime? ActualArrivalDate);

public record ApprovalCallbackRequest(long OaApprovalId, bool Approved, string? Comment);
