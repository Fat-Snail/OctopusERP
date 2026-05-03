using System.Text.Json;

namespace OctopusPLM.Core.Entities;

/// <summary>商品 SKU（库存单位）</summary>
public class ProductSku
{
    public long SkuId { get; set; }
    public long ProductId { get; set; }
    public string SkuCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }

    /// <summary>销售属性值（JSON）：{"颜色":"红色","尺码":"XL","包装方式":"精装"}</summary>
    public string? SaleAttributesJson { get; set; }

    public decimal Price { get; set; }
    public decimal? CostPrice { get; set; }
    public int Stock { get; set; }
    public int Status { get; set; } = 1;

    public Product? Product { get; set; }

    public Dictionary<string, string> GetSaleAttributes() =>
        string.IsNullOrEmpty(SaleAttributesJson)
            ? new()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(SaleAttributesJson) ?? new();

    public void SetSaleAttributes(Dictionary<string, string> attrs) =>
        SaleAttributesJson = JsonSerializer.Serialize(attrs);
}
