using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusPLM.Api.DTOs;
using OctopusPLM.Api.Services;
using OctopusPLM.Infrastructure.Persistence;
using System.Security.Claims;

namespace OctopusPLM.Api.Controllers;

/// <summary>商品管理</summary>
[ApiController]
[Route("api/product")]
[Authorize]
public class ProductController : ControllerBase
{
    private readonly ProductService _service;
    private readonly CategoryService _categoryService;
    private readonly VectorService _vector;
    private readonly CrossSystemService _crossSystem;
    private readonly PlmDbContext _db;

    public ProductController(ProductService service, CategoryService categoryService, VectorService vector,
        CrossSystemService crossSystem, PlmDbContext db)
    {
        _service = service;
        _categoryService = categoryService;
        _vector = vector;
        _crossSystem = crossSystem;
        _db = db;
    }

    private long GetCurrentUserId() =>
        long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private (long Id, string Name) GetCurrentUserInfo()
    {
        var id = long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var name = User.FindFirst("name")?.Value
            ?? User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? id.ToString();
        return (id, name);
    }

    /// <summary>商品列表（分页 + 筛选；可选 withStock / withSales 拉取跨系统聚合字段）</summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] long? categoryId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        [FromQuery] bool withStock = false,
        [FromQuery] bool withSales = false)
    {
        var (rows, total) = await _service.GetListAsync(page, size, categoryId, keyword, status, minPrice, maxPrice, createdFrom, createdTo);

        if (withStock || withSales)
        {
            var ids = rows.Select(r => r.ProductId).ToArray();
            var stockTask = withStock ? _crossSystem.GetStockAsync(ids) : Task.FromResult(new Dictionary<long, StockSummary>());
            var salesTask = withSales ? _crossSystem.GetSalesAsync(ids) : Task.FromResult(new Dictionary<long, SalesSummary>());
            await Task.WhenAll(stockTask, salesTask);

            foreach (var row in rows)
            {
                if (stockTask.Result.TryGetValue(row.ProductId, out var s))
                {
                    row.CrossStockTotal = s.TotalQty;
                    row.CrossStockAvailable = s.AvailableQty;
                }
                if (salesTask.Result.TryGetValue(row.ProductId, out var sa))
                {
                    row.SalesQty30d = sa.SalesQty;
                    row.SalesAmount30d = sa.SalesAmount;
                }
            }
        }

        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    /// <summary>商品详情</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var product = await _service.GetByIdAsync(id);
        if (product == null) return Ok(new { code = 404, msg = "商品不存在" });
        return Ok(new { code = 200, msg = "ok", data = product });
    }

    /// <summary>创建商品</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProductName)) return Ok(new { code = 400, msg = "商品名称不能为空" });
        if (req.CategoryId <= 0) return Ok(new { code = 400, msg = "请选择商品类目" });

        var product = await _service.CreateAsync(req, GetCurrentUserId());

        if (req.VectorizeAfterSave && !string.IsNullOrEmpty(product.MainImage))
            _ = _vector.IndexProductAsync(product.ProductId, product.MainImage, product.ProductName);

        return Ok(new { code = 200, msg = "创建成功", data = product });
    }

    /// <summary>修改商品（仅草稿/已驳回可编辑）</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProductName)) return Ok(new { code = 400, msg = "商品名称不能为空" });
        if (req.CategoryId <= 0) return Ok(new { code = 400, msg = "请选择商品类目" });

        var err = await _service.UpdateAsync(id, req);
        if (err != null) return Ok(new { code = 400, msg = err });

        if (req.VectorizeAfterSave && !string.IsNullOrEmpty(req.MainImage))
            _ = _vector.IndexProductAsync(id, req.MainImage, req.ProductName);

        return Ok(new { code = 200, msg = "修改成功" });
    }

    /// <summary>提交审核（草稿/已驳回 → 待审核）</summary>
    [HttpPut("{id:long}/submit")]
    public async Task<IActionResult> SubmitForReview(long id)
    {
        var err = await _service.SubmitForReviewAsync(id);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "提交审核成功" });
    }

    /// <summary>删除商品（仅草稿可删除）</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var err = await _service.DeleteAsync(id);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>从 1688 JSON 文件批量导入商品</summary>
    [HttpPost("import/1688")]
    [AllowAnonymous]
    public async Task<IActionResult> ImportFrom1688([FromBody] Import1688BatchRequest req)
    {
        if (req.ProductJsonList == null || req.ProductJsonList.Count == 0)
            return Ok(new { code = 400, msg = "productJsonList 不能为空" });

        var result = await _service.ImportFrom1688Async(req.ProductJsonList, _categoryService, GetCurrentUserId());
        return Ok(new { code = 200, msg = $"导入完成：{result.Imported} 成功 / {result.Skipped} 跳过 / {result.Failed} 失败", data = result });
    }

    // ── 审核流程 ──────────────────────────────────────────────────────────────

    /// <summary>商品各状态统计</summary>
    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _service.GetStatsAsync();
        return Ok(new { code = 200, msg = "ok", data = stats });
    }

    /// <summary>审核历史</summary>
    [HttpGet("{id:long}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviews(long id)
    {
        var history = await _service.GetReviewHistoryAsync(id);
        return Ok(new { code = 200, msg = "ok", data = history });
    }

    /// <summary>审核通过（待审核 → 已通过）</summary>
    [HttpPut("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ReviewActionRequest req)
    {
        var (uid, name) = GetCurrentUserInfo();
        var err = await _service.ApproveAsync(id, uid, name, req.Comment);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "审核通过" });
    }

    /// <summary>审核驳回（待审核 → 已驳回）</summary>
    [HttpPut("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] ReviewActionRequest req)
    {
        var (uid, name) = GetCurrentUserInfo();
        var err = await _service.RejectAsync(id, uid, name, req.Comment);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "已驳回" });
    }

    /// <summary>上架（已通过 → 已上架）</summary>
    [HttpPut("{id:long}/publish")]
    public async Task<IActionResult> Publish(long id)
    {
        var (uid, name) = GetCurrentUserInfo();
        var err = await _service.PublishAsync(id, uid, name);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "上架成功" });
    }

    /// <summary>下架（已上架 → 已下架）</summary>
    [HttpPut("{id:long}/discontinue")]
    public async Task<IActionResult> Discontinue(long id, [FromBody] ReviewActionRequest req)
    {
        var (uid, name) = GetCurrentUserInfo();
        var err = await _service.DiscontinueAsync(id, uid, name, req.Comment);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "下架成功" });
    }

    // ── 以图搜商品 ────────────────────────────────────────────────────────────

    /// <summary>以图搜商品：上传图片文件，返回相似商品列表</summary>
    [HttpPost("search/image")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SearchByImage(
        [FromForm] IFormFile image,
        [FromQuery] int limit = 10)
    {
        if (image == null || image.Length == 0)
            return Ok(new { code = 400, msg = "请上传图片" });

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var imageBytes = ms.ToArray();

        var (hits, queryDescription) = await _vector.SearchByImageAsync(imageBytes, Math.Clamp(limit, 1, 50));

        if (hits.Count == 0)
            return Ok(new { code = 200, msg = "ok", data = new { queryDescription, items = Array.Empty<object>() } });

        var productIds = hits.Select(h => h.ProductId).ToList();
        var products = await _service.GetByIdsAsync(productIds);

        var items = hits.Select(h => new
        {
            h.Score,
            h.ImageDescription,
            product = products.FirstOrDefault(p => p.ProductId == h.ProductId)
        }).Where(x => x.product != null).ToList();

        return Ok(new { code = 200, msg = "ok", data = new { queryDescription, items } });
    }

    /// <summary>向量化指定商品（下载主图 → 描述 → 存入 Qdrant）</summary>
    [HttpPost("{id:long}/vectorize")]
    [AllowAnonymous]
    public async Task<IActionResult> VectorizeProduct(long id)
    {
        var product = await _service.GetByIdAsync(id);
        if (product == null) return Ok(new { code = 404, msg = "商品不存在" });
        if (string.IsNullOrEmpty(product.MainImage))
            return Ok(new { code = 400, msg = "商品没有主图，无法向量化" });

        var (ok, description) = await _vector.IndexProductAsync(id, product.MainImage, product.ProductName);
        if (!ok) return Ok(new { code = 500, msg = "向量化失败，请检查 Ollama/Qdrant 服务" });

        return Ok(new { code = 200, msg = "向量化成功", data = new { description } });
    }

    /// <summary>批量向量化所有有主图的商品（后台耗时操作）</summary>
    [HttpPost("vectorize-all")]
    [AllowAnonymous]
    public async Task<IActionResult> VectorizeAll()
    {
        var (rows, total) = await _service.GetListAsync(1, 1000, null, null, null);

        var successCount = 0;
        var failCount = 0;
        foreach (var p in rows.Where(r => !string.IsNullOrEmpty(r.MainImage)))
        {
            var (ok, _) = await _vector.IndexProductAsync(p.ProductId, p.MainImage!, p.ProductName);
            if (ok) successCount++; else failCount++;
        }

        return Ok(new
        {
            code = 200,
            msg = $"批量向量化完成：{successCount} 成功 / {failCount} 失败",
            data = new { total, successCount, failCount }
        });
    }

    // ── P10：跨系统标准化 API ────────────────────────────────────────────────

    /// <summary>批量按 ProductId 查询商品摘要（供 CRM/WMS/MES 跨系统调用）</summary>
    [HttpPost("by-ids")]
    [AllowAnonymous]
    public async Task<IActionResult> ByIds([FromBody] ByIdsRequest req)
    {
        var ids = (req.ProductIds ?? Array.Empty<long>()).Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var rows = await _db.Products
            .Where(p => ids.Contains(p.ProductId))
            .Select(p => new ProductBriefRow
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                CategoryId = p.CategoryId,
                Status = p.Status,
                MainImage = p.MainImage,
                SourceChannel = p.SourceChannel,
            })
            .ToListAsync();

        return Ok(new { code = 200, msg = "ok", data = rows });
    }

    /// <summary>批量按 SourceChannel + SourceId 查询商品（供从渠道反查 PLM 商品）</summary>
    [HttpPost("by-codes")]
    [AllowAnonymous]
    public async Task<IActionResult> ByCodes([FromBody] ByCodesRequest req)
    {
        var codes = (req.Codes ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        if (codes.Length == 0) return Ok(new { code = 200, msg = "ok", data = Array.Empty<object>() });

        var rows = await _db.Products
            .Where(p => p.SourceId != null && codes.Contains(p.SourceId))
            .Select(p => new ProductBriefRow
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                CategoryId = p.CategoryId,
                Status = p.Status,
                MainImage = p.MainImage,
                SourceChannel = p.SourceChannel,
                SourceId = p.SourceId,
            })
            .ToListAsync();

        return Ok(new { code = 200, msg = "ok", data = rows });
    }

    /// <summary>MES 制造产品目录：只返回 PLM 已发布制造版本，供生产订单选择。</summary>
    [HttpGet("manufacturing")]
    [AllowAnonymous]
    public async Task<IActionResult> Manufacturing([FromQuery] string? keyword = null)
    {
        var query = _db.ManufacturingVersions.AsNoTracking().Where(v => v.Status == "released").Select(v => new { v, p = v.Product! });
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.p.ProductName.Contains(keyword) || x.p.ProductId.ToString() == keyword);
        var rows = await query.OrderByDescending(x => x.v.ReleasedAt).Take(200)
            .Select(x => new { productId = x.p.ProductId, manufacturingVersionId = x.v.ManufacturingVersionId, productCode = x.p.SourceId ?? x.p.ProductId.ToString(), productName = x.p.ProductName, version = x.v.Version, status = x.v.Status, bomVersion = x.v.BomVersion, routingVersion = x.v.RoutingVersion, payloadHash = x.v.PayloadHash })
            .ToListAsync();
        return Ok(new { code = 200, msg = "ok", data = rows });
    }

    /// <summary>MES 排程商品选择器：服务端分页搜索已发布制造版本，适配十万级商品。</summary>
    [HttpGet("manufacturing/search")]
    [AllowAnonymous]
    public async Task<IActionResult> ManufacturingSearch([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = _db.Products.AsNoTracking().Where(p => p.Status == "active");
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(p => p.ProductName.StartsWith(term) || (p.SourceId != null && p.SourceId.StartsWith(term)) || p.ProductId.ToString() == term);
        }
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.ProductId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                productId = p.ProductId, productCode = p.SourceId ?? p.ProductId.ToString(), productName = p.ProductName,
                manufacturing = _db.ManufacturingVersions.Where(v => v.ProductId == p.ProductId && v.Status == "released").OrderByDescending(v => v.ReleasedAt)
                    .Select(v => new { manufacturingVersionId = v.ManufacturingVersionId, version = v.Version, status = v.Status, bomVersion = v.BomVersion, routingVersion = v.RoutingVersion, payloadHash = v.PayloadHash, bomItemCount = v.BomItems.Count, operationCount = v.Operations.Count }).FirstOrDefault()
            })
            .ToListAsync();
        return Ok(new { code = 200, msg = "ok", data = new { rows, total, page, pageSize, hasMore = page * pageSize < total } });
    }

    /// <summary>MES 商品浏览与对账目录：真实 PLM active 商品及其最新已发布制造版本。</summary>
    [HttpGet("mes-catalog")]
    [AllowAnonymous]
    public async Task<IActionResult> MesCatalog([FromQuery] string? keyword = null)
    {
        var products = _db.Products.AsNoTracking().Where(x => x.Status == "active");
        if (!string.IsNullOrWhiteSpace(keyword)) products = products.Where(x => x.ProductName.Contains(keyword));
        var rows = await products.OrderByDescending(x => x.UpdatedAt).Take(500).Select(p => new
        {
            productId = p.ProductId,
            productCode = p.SourceId ?? p.ProductId.ToString(),
            productName = p.ProductName,
            productStatus = p.Status,
            manufacturing = _db.ManufacturingVersions.Where(v => v.ProductId == p.ProductId && v.Status == "released").OrderByDescending(v => v.ReleasedAt)
                .Select(v => new { manufacturingVersionId = v.ManufacturingVersionId, version = v.Version, bomVersion = v.BomVersion, routingVersion = v.RoutingVersion, payloadHash = v.PayloadHash, releasedAt = v.ReleasedAt }).FirstOrDefault()
        }).ToListAsync();
        return Ok(new { code = 200, msg = "ok", data = rows });
    }

    /// <summary>商品跨系统聚合视图（PLM 基本信息 + WMS 库存 + CRM 销量）</summary>
    [HttpGet("{id:long}/cross-system")]
    [AllowAnonymous]
    public async Task<IActionResult> CrossSystem(long id, [FromQuery] int salesDays = 30)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.ProductId == id);
        if (p == null) return Ok(new { code = 404, msg = "商品不存在" });

        var ids = new[] { id };
        var stockTask = _crossSystem.GetStockAsync(ids);
        var salesTask = _crossSystem.GetSalesAsync(ids, salesDays);
        await Task.WhenAll(stockTask, salesTask);

        var stock = stockTask.Result.GetValueOrDefault(id);
        var sales = salesTask.Result.GetValueOrDefault(id);

        return Ok(new
        {
            code = 200,
            msg = "ok",
            data = new
            {
                productId = p.ProductId,
                productName = p.ProductName,
                status = p.Status,
                stock = stock == null ? null : new
                {
                    totalQty = stock.TotalQty,
                    availableQty = stock.AvailableQty,
                    lockedQty = stock.LockedQty,
                    warehouseCount = stock.WarehouseCount,
                },
                sales = sales == null ? null : new
                {
                    salesQty = sales.SalesQty,
                    salesAmount = sales.SalesAmount,
                    contractCount = sales.ContractCount,
                    days = sales.Days,
                },
            }
        });
    }
}

public record ByIdsRequest(long[] ProductIds);
public record ByCodesRequest(string[] Codes);

public class ProductBriefRow
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? MainImage { get; set; }
    public string? SourceChannel { get; set; }
    public string? SourceId { get; set; }
}
