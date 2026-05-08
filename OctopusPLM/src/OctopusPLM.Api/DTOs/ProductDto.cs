namespace OctopusPLM.Api.DTOs;

// ── Request DTOs ──

public class CreateProductRequest
{
    public long CategoryId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MainImage { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
    public List<CreateSkuRequest>? Skus { get; set; }
    public List<string>? Images { get; set; }
    /// <summary>保存后自动将主图向量化入 Qdrant</summary>
    public bool VectorizeAfterSave { get; set; }
}

public class CreateSkuRequest
{
    public string SkuCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public Dictionary<string, string>? SaleAttributes { get; set; }
    public decimal Price { get; set; }
    public decimal? CostPrice { get; set; }
    public int Stock { get; set; }
}

public class UpdateProductRequest
{
    public long CategoryId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MainImage { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
    public List<CreateSkuRequest>? Skus { get; set; }
    public List<string>? Images { get; set; }
    /// <summary>保存后自动将主图向量化入 Qdrant</summary>
    public bool VectorizeAfterSave { get; set; }
}

// ── Response DTOs ──

public class ProductResponse
{
    public long ProductId { get; set; }
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MainImage { get; set; }
    public List<string> Images { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public long CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<SkuResponse> Skus { get; set; } = new();
}

public class SkuResponse
{
    public long SkuId { get; set; }
    public string SkuCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public Dictionary<string, string> SaleAttributes { get; set; } = new();
    public decimal Price { get; set; }
    public decimal? CostPrice { get; set; }
    public int Stock { get; set; }
    public int Status { get; set; }
}

public class ProductListRow
{
    public long ProductId { get; set; }
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? MainImage { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public int StatusInt { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public int TotalStock { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ── 审核流程 DTOs ──

public class ReviewActionRequest
{
    public string? Comment { get; set; }
}

public class ReviewHistoryItem
{
    public long ReviewId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public long ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductStatsResponse
{
    public int Total { get; set; }
    public int Draft { get; set; }
    public int PendingReview { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Active { get; set; }
    public int Discontinued { get; set; }
}
