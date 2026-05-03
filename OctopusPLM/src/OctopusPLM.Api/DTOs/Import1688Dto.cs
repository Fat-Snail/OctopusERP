using System.Text.Json.Serialization;

namespace OctopusPLM.Api.DTOs;

// 1688 商品 JSON 最外层结构
public class Ali1688ProductRoot
{
    [JsonPropertyName("productInfo")]
    public Ali1688ProductInfo? ProductInfo { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class Ali1688ProductInfo
{
    [JsonPropertyName("productID")]
    public long ProductID { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("categoryID")]
    public long CategoryID { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("attributes")]
    public List<Ali1688Attribute> Attributes { get; set; } = new();

    [JsonPropertyName("image")]
    public Ali1688Image? Image { get; set; }

    [JsonPropertyName("skuInfos")]
    public List<Ali1688Sku> SkuInfos { get; set; } = new();

    [JsonPropertyName("saleInfo")]
    public Ali1688SaleInfo? SaleInfo { get; set; }

    [JsonPropertyName("supplierLoginId")]
    public string? SupplierLoginId { get; set; }

    [JsonPropertyName("createTime")]
    public string? CreateTime { get; set; }
}

public class Ali1688Attribute
{
    [JsonPropertyName("attributeID")]
    public long AttributeID { get; set; }

    [JsonPropertyName("attributeName")]
    public string AttributeName { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class Ali1688Image
{
    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = new();
}

public class Ali1688Sku
{
    [JsonPropertyName("skuId")]
    public long SkuId { get; set; }

    [JsonPropertyName("skuCode")]
    public string? SkuCode { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("consignPrice")]
    public decimal? ConsignPrice { get; set; }

    [JsonPropertyName("amountOnSale")]
    public int AmountOnSale { get; set; }

    [JsonPropertyName("attributes")]
    public List<Ali1688SkuAttribute> Attributes { get; set; } = new();
}

public class Ali1688SkuAttribute
{
    [JsonPropertyName("attributeID")]
    public long AttributeID { get; set; }

    [JsonPropertyName("attributeDisplayName")]
    public string AttributeDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("attributeValue")]
    public string AttributeValue { get; set; } = string.Empty;
}

public class Ali1688SaleInfo
{
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("minOrderQuantity")]
    public int MinOrderQuantity { get; set; }

    [JsonPropertyName("priceRanges")]
    public List<Ali1688PriceRange> PriceRanges { get; set; } = new();
}

public class Ali1688PriceRange
{
    [JsonPropertyName("startQuantity")]
    public int StartQuantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }
}

// 批量导入请求（接受原始 JSON 文件内容列表）
public class Import1688BatchRequest
{
    /// <summary>每个元素为一条 1688 商品 JSON 原文</summary>
    public List<string> ProductJsonList { get; set; } = new();
}

// 批量导入结果
public class Import1688Result
{
    public int Total { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<Import1688ItemResult> Items { get; set; } = new();
}

public class Import1688ItemResult
{
    public long SourceId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;  // imported / skipped / failed
    public long? ProductId { get; set; }
    public string? Error { get; set; }
}
