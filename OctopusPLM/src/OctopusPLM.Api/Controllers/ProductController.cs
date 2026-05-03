using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.DTOs;
using OctopusPLM.Api.Services;
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

    public ProductController(ProductService service, CategoryService categoryService, VectorService vector)
    {
        _service = service;
        _categoryService = categoryService;
        _vector = vector;
    }

    private long GetCurrentUserId() =>
        long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>商品列表（分页 + 筛选）</summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] long? categoryId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null)
    {
        var (rows, total) = await _service.GetListAsync(page, size, categoryId, keyword, status);
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
}
