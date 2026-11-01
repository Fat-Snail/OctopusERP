using System.Security.Cryptography;
using System.Security.Claims;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusPLM.Api.Services;
using OctopusPLM.Core.Entities;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/manufacturing")]
public sealed class ManufacturingController(PlmDbContext db, ManufacturingBomService bomService, IHttpClientFactory httpFactory, IConfiguration config, ILogger<ManufacturingController> logger) : ControllerBase
{
    public sealed record BomItemRequest(int LineNo, long? MaterialId, string? MaterialCode, string? MaterialName, decimal Quantity, decimal LossRate);
    public sealed record OperationRequest(int Sequence, string OperationCode, string OperationName, string RequiredCapability, int StandardCycleSec, int SetupMinutes, bool IsSplittable = true);
    public sealed record CreateVersionRequest(long ProductId, string Version, string BomVersion, string RoutingVersion, List<BomItemRequest> BomItems, List<OperationRequest> Operations);
    public sealed record ApplyTemplateRequest(long TemplateId);
    public sealed record ExtractTemplateRequest(string TemplateName, string Version);

    private long UserId() => long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private string UserName() => User.FindFirst("name")?.Value ?? User.Identity?.Name ?? UserId().ToString();

    [HttpGet("versions")]
    public async Task<IActionResult> List([FromQuery] long? productId, CancellationToken ct)
    {
        var query = db.ManufacturingVersions.AsNoTracking().Include(x => x.Product).Include(x => x.BomItems).Include(x => x.Operations).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        var entities = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var rows = entities.Select(x =>
        {
            var validationIssues = ManufacturingReleaseValidator.Validate(x);
            return new { x.ManufacturingVersionId, x.ProductId, x.Product!.ProductName, x.Version, x.Status, x.BomVersion, x.RoutingVersion, bomItemCount = x.BomItems.Count, operationCount = x.Operations.Count, x.PayloadHash, x.CreatedAt, x.ReleasedAt, x.MesSyncStatus, x.LastMesSyncAt, x.MesSyncError, x.MesNotifyCount, releaseReady = validationIssues.Count == 0, validationIssues };
        }).ToList();
        return Ok(new { code = 200, msg = "ok", data = rows });
    }

    [HttpGet("versions/{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var version = await db.ManufacturingVersions.AsNoTracking()
            .Include(x => x.BomItems).Include(x => x.Operations)
            .FirstOrDefaultAsync(x => x.ManufacturingVersionId == id, ct);
        if (version is null) return Ok(new { code = 404, msg = "制造版本不存在", data = (object?)null });
        var validationIssues = ManufacturingReleaseValidator.Validate(version);
        return Ok(new
        {
            code = 200,
            msg = "ok",
            data = new
            {
                version.ManufacturingVersionId,
                version.ProductId,
                version.Version,
                version.Status,
                version.BomVersion,
                version.RoutingVersion,
                bomItems = version.BomItems.OrderBy(x => x.LineNo),
                operations = version.Operations.OrderBy(x => x.Sequence),
                releaseReady = validationIssues.Count == 0,
                validationIssues
            }
        });
    }

    [HttpPost("versions")]
    public async Task<IActionResult> Create(CreateVersionRequest req, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(x => x.ProductId == req.ProductId, ct)) return Ok(new { code = 400, msg = "PLM 产品不存在", data = (object?)null });
        if (string.IsNullOrWhiteSpace(req.Version)) return Ok(new { code = 400, msg = "制造版本不能为空", data = (object?)null });
        if (await db.ManufacturingVersions.AnyAsync(x => x.ProductId == req.ProductId && x.Version == req.Version, ct)) return Ok(new { code = 400, msg = "制造版本已存在", data = (object?)null });
        var version = new ManufacturingVersion { ProductId = req.ProductId, Version = req.Version, BomVersion = req.BomVersion, RoutingVersion = req.RoutingVersion, CreatedAt = DateTime.UtcNow };
        version.BomItems = await bomService.ResolveItemsAsync(req.BomItems.Select(x => new BomMaterialInput(x.LineNo, x.MaterialId, x.MaterialCode, x.MaterialName, x.Quantity, x.LossRate)), ct);
        version.Operations = req.Operations.Select(x => new ManufacturingOperation { Sequence = x.Sequence, OperationCode = x.OperationCode, OperationName = x.OperationName, RequiredCapability = x.RequiredCapability, StandardCycleSec = x.StandardCycleSec, SetupMinutes = x.SetupMinutes, IsSplittable = x.IsSplittable }).ToList();
        db.ManufacturingVersions.Add(version); await db.SaveChangesAsync(ct);
        return Ok(new { code = 200, msg = "制造版本草稿已创建", data = version });
    }

    [HttpPut("versions/{id:long}")]
    public async Task<IActionResult> Update(long id, CreateVersionRequest req, CancellationToken ct)
    {
        var version = await db.ManufacturingVersions.Include(x => x.BomItems).Include(x => x.Operations)
            .FirstOrDefaultAsync(x => x.ManufacturingVersionId == id, ct);
        if (version is null) return Ok(new { code = 404, msg = "制造版本不存在", data = (object?)null });
        if (version.Status != "draft") return Ok(new { code = 400, msg = "已发布制造版本不可修改，请新建版本", data = (object?)null });
        if (req.ProductId != version.ProductId) return Ok(new { code = 400, msg = "制造版本所属商品不可更换", data = (object?)null });
        if (string.IsNullOrWhiteSpace(req.Version)) return Ok(new { code = 400, msg = "制造版本不能为空", data = (object?)null });
        if (await db.ManufacturingVersions.AnyAsync(x => x.ManufacturingVersionId != id && x.ProductId == req.ProductId && x.Version == req.Version, ct))
            return Ok(new { code = 400, msg = "制造版本已存在", data = (object?)null });

        version.Version = req.Version;
        version.BomVersion = req.BomVersion;
        version.RoutingVersion = req.RoutingVersion;
        db.ManufacturingBomItems.RemoveRange(version.BomItems);
        db.ManufacturingOperations.RemoveRange(version.Operations);
        version.BomItems = await bomService.ResolveItemsAsync(req.BomItems.Select(x => new BomMaterialInput(x.LineNo, x.MaterialId, x.MaterialCode, x.MaterialName, x.Quantity, x.LossRate)), ct);
        version.Operations = req.Operations.Select(x => new ManufacturingOperation { Sequence = x.Sequence, OperationCode = x.OperationCode, OperationName = x.OperationName, RequiredCapability = x.RequiredCapability, StandardCycleSec = x.StandardCycleSec, SetupMinutes = x.SetupMinutes, IsSplittable = x.IsSplittable }).ToList();
        await db.SaveChangesAsync(ct);
        return Ok(new { code = 200, msg = "制造版本草稿已保存", data = version });
    }

    [HttpPost("versions/{id:long}/release")]
    public async Task<IActionResult> Release(long id, CancellationToken ct)
    {
        var version = await db.ManufacturingVersions.Include(x => x.BomItems).Include(x => x.Operations).FirstOrDefaultAsync(x => x.ManufacturingVersionId == id, ct);
        if (version is null) return Ok(new { code = 404, msg = "制造版本不存在", data = (object?)null });
        if (version.Status != "draft") return Ok(new { code = 400, msg = "只有草稿制造版本可以发布", data = (object?)null });
        var validationIssues = ManufacturingReleaseValidator.Validate(version);
        if (validationIssues.Count > 0)
            return Ok(new { code = 422, msg = "制造资料不完整，请补齐红色标注字段", data = new { releaseReady = false, validationIssues } });
        var canonical = JsonSerializer.Serialize(new { version.ProductId, version.Version, version.BomVersion, version.RoutingVersion, bom = version.BomItems.OrderBy(x => x.LineNo).Select(x => new { x.LineNo, x.MaterialCode, x.Quantity, x.LossRate }), operations = version.Operations.OrderBy(x => x.Sequence).Select(x => new { x.Sequence, x.OperationCode, x.RequiredCapability, x.StandardCycleSec, x.SetupMinutes, x.IsSplittable }) });
        version.PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        version.Status = "released"; version.ReleasedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        await NotifyMesAsync(version, false, ct); await db.SaveChangesAsync(ct);
        return Ok(new { code = 200, msg = version.MesSyncStatus == "synced" ? "制造版本已发布并同步 MES" : $"制造版本已发布，但 MES 同步失败：{version.MesSyncError}", data = version });
    }

    [HttpPost("versions/{id:long}/apply-bom-template"), Authorize(Policy = "BomTemplateApply")]
    public async Task<IActionResult> ApplyTemplate(long id, ApplyTemplateRequest request, CancellationToken ct)
    {
        try { await bomService.ApplyTemplateAsync(id, request.TemplateId, UserId(), UserName(), ct); return Ok(new { code = 200, msg = "BOM 模板已套用" }); }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or MaterialConflictException) { return Ok(new { code = 400, msg = ex.Message }); }
    }

    [HttpPost("versions/{id:long}/extract-bom-template"), Authorize(Policy = "BomTemplateExtract")]
    public async Task<IActionResult> ExtractTemplate(long id, ExtractTemplateRequest request, CancellationToken ct)
    {
        try { return Ok(new { code = 200, msg = "已提取为类目 BOM 模板草稿", data = await bomService.ExtractAsync(id, request.TemplateName, request.Version, UserId(), UserName(), ct) }); }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or MaterialConflictException) { return Ok(new { code = 400, msg = ex.Message }); }
    }

    [HttpPost("versions/{id:long}/notify-mes")]
    public async Task<IActionResult> NotifyMes(long id, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        var version = await db.ManufacturingVersions.FirstOrDefaultAsync(x => x.ManufacturingVersionId == id, ct);
        if (version is null) return Ok(new { code = 404, msg = "制造版本不存在", data = (object?)null });
        if (version.Status != "released") return Ok(new { code = 400, msg = "只有已发布制造版本可以通知 MES", data = (object?)null });
        if (version.MesSyncStatus == "synced" && !force) return Ok(new { code = 409, msg = "该版本已经同步 MES；如需覆盖检查，请选择强制更新", data = version });
        await NotifyMesAsync(version, force, ct); await db.SaveChangesAsync(ct);
        return Ok(new { code = version.MesSyncStatus == "synced" ? 200 : 500, msg = version.MesSyncStatus == "synced" ? (force ? "已强制更新 MES" : "已重新通知 MES") : version.MesSyncError, data = version });
    }

    private async Task NotifyMesAsync(ManufacturingVersion version, bool force, CancellationToken ct)
    {
        version.MesSyncStatus = "syncing"; version.MesNotifyCount++; version.MesSyncError = null;
        try
        {
            var client = httpFactory.CreateClient(); client.Timeout = TimeSpan.FromSeconds(10);
            var sharedSecret = config["MesIntegration:SharedSecret"] ?? "plm-mes-manufacturing-secret-dev";
            client.DefaultRequestHeaders.Add("X-PLM-Secret", sharedSecret);
            var suffix = force ? $"FORCE-{DateTime.UtcNow:yyyyMMddHHmmssfff}" : version.PayloadHash![..12];
            var sentAt = DateTime.UtcNow;
            var eventId = $"PLM-REL-{version.ManufacturingVersionId}-{suffix}";
            var evt = new
            {
                eventId,
                eventType = force ? "manufacturing_version_force_sync" : "manufacturing_version_released",
                productId = version.ProductId,
                manufacturingVersionId = version.ManufacturingVersionId,
                releasedAt = version.ReleasedAt,
                sentAt,
                payloadHash = version.PayloadHash,
            };
            var canonical = $"{eventId}\n{version.ProductId}\n{version.ManufacturingVersionId}\n{sentAt:O}\n{version.PayloadHash}";
            var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(sharedSecret), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            client.DefaultRequestHeaders.Add("X-PLM-Signature", signature);
            var response = await client.PostAsJsonAsync($"{config["MesIntegration:BaseUrl"] ?? "http://localhost:5006"}/api/integration/plm/manufacturing-released", evt, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var ack = JsonSerializer.Deserialize<MesAck>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (!response.IsSuccessStatusCode || ack?.Code != 200) throw new InvalidOperationException(ack?.Msg ?? $"MES HTTP {(int)response.StatusCode}");
            version.MesSyncStatus = "synced"; version.LastMesSyncAt = DateTime.UtcNow;
        }
        catch (Exception ex) { version.MesSyncStatus = "failed"; version.MesSyncError = ex.Message; logger.LogWarning(ex, "MES 制造版本通知失败"); }
    }

    private sealed record MesAck(int Code, string? Msg);

    [AllowAnonymous]
    [HttpGet("products/{productId:long}/versions/{versionId:long}")]
    public async Task<IActionResult> GetReleased(long productId, long versionId, CancellationToken ct)
    {
        var version = await db.ManufacturingVersions.AsNoTracking().Include(x => x.Product).Include(x => x.BomItems).Include(x => x.Operations)
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.ManufacturingVersionId == versionId && x.Status == "released", ct);
        if (version is null) return Ok(new { code = 404, msg = "已发布制造版本不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = new { product = new { version.ProductId, productCode = version.Product!.SourceId ?? version.ProductId.ToString(), version.Product.ProductName, version = version.Version }, manufacturingVersionId = version.ManufacturingVersionId, version.BomVersion, version.RoutingVersion, version.PayloadHash, version.ReleasedAt, bomItems = version.BomItems.OrderBy(x => x.LineNo), operations = version.Operations.OrderBy(x => x.Sequence) } });
    }
}
