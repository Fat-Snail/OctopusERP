namespace OctopusOMS.Api.Persistence;

/// <summary>渠道：淘宝/天猫/京东/拼多多/抖音/Shopify/Amazon</summary>
public class OmsChannel
{
    public long ChannelId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    /// <summary>marketplace（电商平台）/ self_built（独立站）/ offline（线下）</summary>
    public string ChannelType { get; set; } = "marketplace";
    public string Status { get; set; } = "active";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>店铺（一个渠道下可有多个店铺）</summary>
public class OmsShop
{
    public long ShopId { get; set; }
    public long ChannelId { get; set; }
    public string ShopCode { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string? Region { get; set; }       // 国内 / 海外
    public string? Currency { get; set; } = "CNY";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }

    public OmsChannel? Channel { get; set; }
}

/// <summary>外部订单（拉取自渠道平台或人工录入）</summary>
public class OmsOrder
{
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public long ShopId { get; set; }
    public long? ChannelId { get; set; }
    public string ExternalOrderNo { get; set; } = string.Empty;  // 平台订单号
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? ShipAddress { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    /// <summary>created / paid / processing / shipped / completed / cancelled / refunded</summary>
    public string Status { get; set; } = "created";
    public DateTime OrderDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>关联 WMS 出库单</summary>
    public long? WmsOutboundOrderId { get; set; }
    /// <summary>关联 CRM 合同（B2B 订单）</summary>
    public long? CrmContractId { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public OmsShop? Shop { get; set; }
    public List<OmsOrderItem> Items { get; set; } = new();
}

/// <summary>订单明细</summary>
public class OmsOrderItem
{
    public long ItemId { get; set; }
    public long OrderId { get; set; }
    /// <summary>渠道 SKU 编码（平台原始编码）</summary>
    public string ChannelSkuCode { get; set; } = string.Empty;
    /// <summary>映射到 PLM 的 ProductId（通过 sku_mapping 解析）</summary>
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public OmsOrder? Order { get; set; }
}

/// <summary>SKU 映射（渠道 SKU → PLM ProductId）</summary>
public class OmsSkuMapping
{
    public long MappingId { get; set; }
    public long ChannelId { get; set; }
    public string ChannelSkuCode { get; set; } = string.Empty;
    public long PlmProductId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>售后单（退/换/补/退款）</summary>
public class OmsAfterSale
{
    public long AfterSaleId { get; set; }
    public string AfterSaleNo { get; set; } = string.Empty;
    public long OrderId { get; set; }
    /// <summary>refund（退款）/ return（退货）/ exchange（换货）/ supplement（补发）</summary>
    public string AfterSaleType { get; set; } = "refund";
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>pending / approved / rejected / completed</summary>
    public string Status { get; set; } = "pending";
    public string? CustomerNote { get; set; }
    public string? OperatorNote { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class OmsSyncUser
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public DateTime LastSyncAt { get; set; }
}
