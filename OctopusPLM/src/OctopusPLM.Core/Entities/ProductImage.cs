namespace OctopusPLM.Core.Entities;

/// <summary>商品图片（独立子表，为以图搜图预留向量字段）</summary>
public class ProductImage
{
    public long ImageId { get; set; }
    public long ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public bool IsMain { get; set; }
    public int OrderNum { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>特征向量 MD5 指纹（以图搜图预留）</summary>
    public string? FeatureVectorHash { get; set; }

    /// <summary>特征向量维度（预留）</summary>
    public int? FeatureVectorDim { get; set; }

    public Product? Product { get; set; }
}
