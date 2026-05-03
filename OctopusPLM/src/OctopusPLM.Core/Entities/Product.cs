using System.Text.Json;

namespace OctopusPLM.Core.Entities;

/// <summary>商品 SPU</summary>
public class Product
{
    public long ProductId { get; set; }
    public long CategoryId { get; set; }
    public long? ModelVersionId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MainImage { get; set; }
    public string? ImagesJson { get; set; }

    /// <summary>动态属性键值对（JSON）</summary>
    public string? AttributesJson { get; set; }

    /// <summary>来源渠道，如 "1688" / "taobao"，null 表示手动录入</summary>
    public string? SourceChannel { get; set; }
    /// <summary>来源渠道商品 ID（用于防重和溯源）</summary>
    public string? SourceId { get; set; }
    /// <summary>来源渠道类目名称（冗余存储，方便溯源）</summary>
    public string? SourceCategoryName { get; set; }

    /// <summary>draft / pending_review / approved / rejected / active / discontinued</summary>
    public string Status { get; set; } = "draft";
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Category? Category { get; set; }
    public CategoryModelVersion? ModelVersion { get; set; }
    public List<ProductImage> Images { get; set; } = new();
    public List<ProductSku> Skus { get; set; } = new();
    public List<ProductReview> Reviews { get; set; } = new();
    public List<ProductAttributeValue> AttributeValues { get; set; } = new();
    public List<ProductDetailBlock> DetailBlocks { get; set; } = new();

    public Dictionary<string, string> GetAttributes() =>
        string.IsNullOrEmpty(AttributesJson)
            ? new()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(AttributesJson) ?? new();

    public void SetAttributes(Dictionary<string, string> attrs) =>
        AttributesJson = JsonSerializer.Serialize(attrs);
}
