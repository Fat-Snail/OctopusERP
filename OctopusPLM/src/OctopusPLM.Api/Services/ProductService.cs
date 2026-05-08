using Microsoft.EntityFrameworkCore;
using OctopusPLM.Api.DTOs;
using OctopusPLM.Core.Entities;
using OctopusPLM.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctopusPLM.Api.Services;

public class ProductService
{
    private readonly PlmDbContext _db;

    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        ["draft"] = "草稿",
        ["pending_review"] = "待审核",
        ["approved"] = "已通过",
        ["rejected"] = "已驳回",
        ["active"] = "已上架",
        ["discontinued"] = "已下架",
    };

    public ProductService(PlmDbContext db) => _db = db;

    /// <summary>商品列表（分页 + 筛选）</summary>
    public async Task<(List<ProductListRow> Rows, int Total)> GetListAsync(
        int page, int size, long? categoryId, string? keyword, string? status)
    {
        var query = _db.Products.AsQueryable();

        if (categoryId.HasValue && categoryId > 0)
            query = query.Where(p => p.CategoryId == categoryId.Value);
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(p => p.ProductName.Contains(keyword));
        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);

        var total = await query.CountAsync();

        var products = await query.OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * size).Take(size)
            .Include(p => p.Category)
            .Include(p => p.Skus)
            .ToListAsync();

        var rows = products.Select(p => new ProductListRow
        {
            ProductId = p.ProductId,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? "",
            ProductName = p.ProductName,
            MainImage = p.MainImage,
            Status = p.Status,
            StatusLabel = StatusLabels.GetValueOrDefault(p.Status, p.Status),
            StatusInt = p.Status switch { "active" => 1, "draft" => 0, "pending_review" => 2, _ => 0 },
            MinPrice = p.Skus.Count > 0 ? p.Skus.Min(s => s.Price) : 0,
            MaxPrice = p.Skus.Count > 0 ? p.Skus.Max(s => s.Price) : 0,
            TotalStock = p.Skus.Sum(s => s.Stock),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        }).ToList();

        return (rows, total);
    }

    /// <summary>商品详情</summary>
    public async Task<ProductResponse?> GetByIdAsync(long id)
    {
        var p = await _db.Products
            .Include(x => x.Category)
            .Include(x => x.Images)
            .Include(x => x.Skus)
            .Include(x => x.AttributeValues)
            .FirstOrDefaultAsync(x => x.ProductId == id);

        if (p == null) return null;

        return MapToResponse(p);
    }

    /// <summary>批量查询商品（用于向量搜索结果还原）</summary>
    public async Task<List<ProductListRow>> GetByIdsAsync(List<long> ids)
    {
        var products = await _db.Products
            .Where(p => ids.Contains(p.ProductId))
            .Include(p => p.Category)
            .Include(p => p.Skus)
            .ToListAsync();

        return products.Select(p => new ProductListRow
        {
            ProductId = p.ProductId,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? "",
            ProductName = p.ProductName,
            MainImage = p.MainImage,
            Status = p.Status,
            StatusLabel = StatusLabels.GetValueOrDefault(p.Status, p.Status),
            StatusInt = p.Status switch { "active" => 1, "draft" => 0, "pending_review" => 2, _ => 0 },
            MinPrice = p.Skus.Count > 0 ? p.Skus.Min(s => s.Price) : 0,
            MaxPrice = p.Skus.Count > 0 ? p.Skus.Max(s => s.Price) : 0,
            TotalStock = p.Skus.Sum(s => s.Stock),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        }).ToList();
    }

    /// <summary>创建商品</summary>
    public async Task<ProductResponse> CreateAsync(CreateProductRequest req, long userId)
    {
        var product = new Product
        {
            CategoryId = req.CategoryId,
            ProductName = req.ProductName,
            Description = req.Description,
            MainImage = req.MainImage,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (req.Attributes != null)
        {
            product.SetAttributes(req.Attributes);
            await AddAttributeValuesAsync(product, req.Attributes);
        }

        if (req.Skus != null)
        {
            foreach (var s in req.Skus)
            {
                var sku = new ProductSku
                {
                    SkuCode = s.SkuCode,
                    Barcode = s.Barcode,
                    Price = s.Price,
                    CostPrice = s.CostPrice,
                    Stock = s.Stock,
                };
                if (s.SaleAttributes != null) sku.SetSaleAttributes(s.SaleAttributes);
                product.Skus.Add(sku);
            }
        }

        if (req.Images != null)
        {
            for (int i = 0; i < req.Images.Count; i++)
            {
                product.Images.Add(new ProductImage
                {
                    Url = req.Images[i],
                    IsMain = i == 0 && string.IsNullOrEmpty(req.MainImage),
                    OrderNum = i,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return MapToResponse(product);
    }

    /// <summary>更新商品（仅草稿/已驳回可编辑）</summary>
    public async Task<string?> UpdateAsync(long id, UpdateProductRequest req)
    {
        var product = await _db.Products
            .Include(p => p.Skus)
            .Include(p => p.Images)
            .Include(p => p.AttributeValues)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null) return "商品不存在";
        if (product.Status != "draft" && product.Status != "rejected") return "仅草稿/已驳回状态的商品可编辑";

        product.CategoryId = req.CategoryId;
        product.ProductName = req.ProductName;
        product.Description = req.Description;
        product.MainImage = req.MainImage;
        product.UpdatedAt = DateTime.UtcNow;

        if (req.Attributes != null)
        {
            product.SetAttributes(req.Attributes);
            _db.ProductAttributeValues.RemoveRange(product.AttributeValues);
            product.AttributeValues.Clear();
            await AddAttributeValuesAsync(product, req.Attributes);
        }

        // 替换 SKU
        if (req.Skus != null)
        {
            _db.ProductSkus.RemoveRange(product.Skus);
            product.Skus.Clear();
            foreach (var s in req.Skus)
            {
                var sku = new ProductSku
                {
                    SkuCode = s.SkuCode,
                    Barcode = s.Barcode,
                    Price = s.Price,
                    CostPrice = s.CostPrice,
                    Stock = s.Stock,
                };
                if (s.SaleAttributes != null) sku.SetSaleAttributes(s.SaleAttributes);
                product.Skus.Add(sku);
            }
        }

        // 替换图片
        if (req.Images != null)
        {
            _db.ProductImages.RemoveRange(product.Images);
            product.Images.Clear();
            for (int i = 0; i < req.Images.Count; i++)
            {
                product.Images.Add(new ProductImage
                {
                    Url = req.Images[i],
                    IsMain = i == 0 && string.IsNullOrEmpty(req.MainImage),
                    OrderNum = i,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>提交审核（草稿/已驳回 → 待审核）</summary>
    public async Task<string?> SubmitForReviewAsync(long id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return "商品不存在";
        if (product.Status != "draft" && product.Status != "rejected") return "仅草稿/已驳回状态可提交审核";

        product.Status = "pending_review";
        product.UpdatedAt = DateTime.UtcNow;
        product.Reviews.Add(new ProductReview
        {
            ReviewerId = product.CreatedBy,
            ReviewerName = "",
            Action = "submit",
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return null;
    }

    private static readonly Dictionary<string, string> ActionLabels = new()
    {
        ["submit"] = "提交审核",
        ["approve"] = "审核通过",
        ["reject"] = "审核驳回",
        ["publish"] = "上架",
        ["discontinue"] = "下架",
        ["cancel"] = "撤回",
    };

    /// <summary>审核通过（待审核 → 已通过）</summary>
    public async Task<string?> ApproveAsync(long id, long reviewerId, string reviewerName, string? comment)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return "商品不存在";
        if (product.Status != "pending_review") return "仅待审核状态可通过审核";

        product.Status = "approved";
        product.UpdatedAt = DateTime.UtcNow;
        product.Reviews.Add(new ProductReview
        {
            ReviewerId = reviewerId,
            ReviewerName = reviewerName,
            Action = "approve",
            Comment = comment,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>审核驳回（待审核 → 已驳回）</summary>
    public async Task<string?> RejectAsync(long id, long reviewerId, string reviewerName, string? comment)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return "商品不存在";
        if (product.Status != "pending_review") return "仅待审核状态可驳回";

        product.Status = "rejected";
        product.UpdatedAt = DateTime.UtcNow;
        product.Reviews.Add(new ProductReview
        {
            ReviewerId = reviewerId,
            ReviewerName = reviewerName,
            Action = "reject",
            Comment = comment,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>上架（已通过 → 已上架）</summary>
    public async Task<string?> PublishAsync(long id, long reviewerId, string reviewerName)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return "商品不存在";
        if (product.Status != "approved") return "仅已通过状态可上架";

        product.Status = "active";
        product.UpdatedAt = DateTime.UtcNow;
        product.Reviews.Add(new ProductReview
        {
            ReviewerId = reviewerId,
            ReviewerName = reviewerName,
            Action = "publish",
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>下架（已上架 → 已下架）</summary>
    public async Task<string?> DiscontinueAsync(long id, long reviewerId, string reviewerName, string? comment)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return "商品不存在";
        if (product.Status != "active") return "仅已上架状态可下架";

        product.Status = "discontinued";
        product.UpdatedAt = DateTime.UtcNow;
        product.Reviews.Add(new ProductReview
        {
            ReviewerId = reviewerId,
            ReviewerName = reviewerName,
            Action = "discontinue",
            Comment = comment,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>获取商品审核历史</summary>
    public async Task<List<ReviewHistoryItem>> GetReviewHistoryAsync(long id)
    {
        var rows = await _db.ProductReviews
            .Where(r => r.ProductId == id)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        return rows.Select(r => new ReviewHistoryItem
        {
            ReviewId = r.ReviewId,
            Action = r.Action,
            ActionLabel = ActionLabels.GetValueOrDefault(r.Action, r.Action),
            ReviewerId = r.ReviewerId,
            ReviewerName = r.ReviewerName,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt,
        }).ToList();
    }

    /// <summary>商品各状态统计</summary>
    public async Task<ProductStatsResponse> GetStatsAsync()
    {
        var counts = await _db.Products
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var dict = counts.ToDictionary(x => x.Status, x => x.Count);
        return new ProductStatsResponse
        {
            Total = counts.Sum(x => x.Count),
            Draft = dict.GetValueOrDefault("draft"),
            PendingReview = dict.GetValueOrDefault("pending_review"),
            Approved = dict.GetValueOrDefault("approved"),
            Rejected = dict.GetValueOrDefault("rejected"),
            Active = dict.GetValueOrDefault("active"),
            Discontinued = dict.GetValueOrDefault("discontinued"),
        };
    }

    /// <summary>删除商品（仅草稿）</summary>
    public async Task<string?> DeleteAsync(long id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return "商品不存在";
        if (product.Status != "draft") return "仅草稿状态的商品可删除";

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return null;
    }

    // ── 辅助映射 ──

    private async Task AddAttributeValuesAsync(Product product, Dictionary<string, string> attrs)
    {
        if (attrs.Count == 0) return;

        var keys = attrs.Keys.ToList();
        var attributeDefs = await _db.Attributes
            .Where(a => keys.Contains(a.Name) || (a.Code != null && keys.Contains(a.Code)))
            .ToListAsync();

        foreach (var attr in attributeDefs)
        {
            var rawValue = attrs.TryGetValue(attr.Name, out var byName)
                ? byName
                : attr.Code != null && attrs.TryGetValue(attr.Code, out var byCode)
                    ? byCode
                    : null;

            if (rawValue == null) continue;

            var value = new ProductAttributeValue
            {
                AttributeId = attr.AttributeId,
                ValueText = rawValue,
            };

            if (attr.AttributeType == "number" && decimal.TryParse(rawValue, out var number))
                value.ValueNumber = number;
            if (attr.AttributeType == "date" && DateTime.TryParse(rawValue, out var dateTime))
                value.ValueDateTime = dateTime;

            product.AttributeValues.Add(value);
        }
    }

    // ── 1688 批量导入 ──────────────────────────────────────────────────────

    private const string CdnBase = "https://cbu01.alicdn.com/";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<Import1688Result> ImportFrom1688Async(
        List<string> jsonList, CategoryService categoryService, long userId)
    {
        var result = new Import1688Result { Total = jsonList.Count };

        foreach (var raw in jsonList)
        {
            Ali1688ProductRoot? root;
            try { root = JsonSerializer.Deserialize<Ali1688ProductRoot>(raw, JsonOpts); }
            catch (Exception ex)
            {
                result.Failed++;
                result.Items.Add(new Import1688ItemResult { Status = "failed", Error = $"JSON解析失败: {ex.Message}" });
                continue;
            }

            var pi = root?.ProductInfo;
            if (pi == null || !root!.Success)
            {
                result.Failed++;
                result.Items.Add(new Import1688ItemResult { Status = "failed", Error = "success=false 或 productInfo 为空" });
                continue;
            }

            var sourceId = pi.ProductID.ToString();

            // 防重：同渠道同 sourceId 已存在则跳过
            var exists = await _db.Products.AnyAsync(p => p.SourceChannel == "1688" && p.SourceId == sourceId);
            if (exists)
            {
                result.Skipped++;
                result.Items.Add(new Import1688ItemResult
                {
                    SourceId = pi.ProductID, Subject = pi.Subject,
                    Status = "skipped", Error = "已存在，跳过"
                });
                continue;
            }

            try
            {
                // 类目：按名称 FindOrCreate
                var catName = !string.IsNullOrEmpty(pi.CategoryName) ? pi.CategoryName : "1688其他";
                var categoryId = await categoryService.FindOrCreateByNameAsync(catName);

                // 图片：补全 CDN 前缀
                var rawImages = pi.Image?.Images ?? new();
                var images = rawImages.Select(img =>
                    img.StartsWith("http") ? img : CdnBase + img).ToList();
                var mainImage = images.FirstOrDefault();

                // 属性：同名属性多值用 ";" 拼接
                var attrDict = new Dictionary<string, string>();
                foreach (var a in pi.Attributes)
                {
                    var key = a.AttributeName;
                    if (string.IsNullOrEmpty(key)) continue;
                    if (attrDict.TryGetValue(key, out var prev))
                        attrDict[key] = prev + ";" + a.Value;
                    else
                        attrDict[key] = a.Value;
                }

                var product = new Product
                {
                    CategoryId = categoryId,
                    ProductName = pi.Subject.Length > 300 ? pi.Subject[..300] : pi.Subject,
                    Description = pi.Description,
                    MainImage = mainImage,
                    SourceChannel = "1688",
                    SourceId = sourceId,
                    SourceCategoryName = pi.CategoryName,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Status = "draft",
                };
                product.SetAttributes(attrDict);

                // 图片子表
                for (int i = 0; i < images.Count; i++)
                    product.Images.Add(new ProductImage
                    {
                        Url = images[i],
                        IsMain = i == 0,
                        OrderNum = i,
                        CreatedAt = DateTime.UtcNow,
                    });

                // SKU
                if (pi.SkuInfos.Count > 0)
                {
                    foreach (var sku in pi.SkuInfos)
                    {
                        var saleAttrs = sku.Attributes
                            .Where(a => !string.IsNullOrEmpty(a.AttributeDisplayName))
                            .ToDictionary(a => a.AttributeDisplayName, a => a.AttributeValue);

                        var productSku = new ProductSku
                        {
                            SkuCode = !string.IsNullOrEmpty(sku.SkuCode) ? sku.SkuCode : sku.SkuId.ToString(),
                            Price = sku.Price,
                            CostPrice = sku.ConsignPrice,
                            Stock = sku.AmountOnSale,
                            Status = 1,
                        };
                        productSku.SetSaleAttributes(saleAttrs);
                        product.Skus.Add(productSku);
                    }
                }
                else
                {
                    // 无 SKU 时用阶梯价第一档兜底
                    var basePrice = pi.SaleInfo?.PriceRanges.FirstOrDefault()?.Price ?? 0m;
                    product.Skus.Add(new ProductSku
                    {
                        SkuCode = sourceId,
                        Price = basePrice,
                        Stock = 0,
                        Status = 1,
                    });
                }

                _db.Products.Add(product);
                await _db.SaveChangesAsync();

                result.Imported++;
                result.Items.Add(new Import1688ItemResult
                {
                    SourceId = pi.ProductID, Subject = pi.Subject,
                    Status = "imported", ProductId = product.ProductId
                });
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Items.Add(new Import1688ItemResult
                {
                    SourceId = pi.ProductID, Subject = pi.Subject,
                    Status = "failed", Error = ex.Message
                });
            }
        }

        return result;
    }

    private static ProductResponse MapToResponse(Product p)
    {
        return new ProductResponse
        {
            ProductId = p.ProductId,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? "",
            ProductName = p.ProductName,
            Description = p.Description,
            MainImage = p.MainImage,
            Images = p.Images.OrderBy(i => i.OrderNum).Select(i => i.Url).ToList(),
            Attributes = p.GetAttributes(),
            Status = p.Status,
            StatusLabel = StatusLabels.GetValueOrDefault(p.Status, p.Status),
            CreatedBy = p.CreatedBy,
            CreatedByName = "",
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            Skus = p.Skus.Select(s => new SkuResponse
            {
                SkuId = s.SkuId,
                SkuCode = s.SkuCode,
                Barcode = s.Barcode,
                SaleAttributes = s.GetSaleAttributes(),
                Price = s.Price,
                CostPrice = s.CostPrice,
                Stock = s.Stock,
                Status = s.Status,
            }).ToList(),
        };
    }
}
