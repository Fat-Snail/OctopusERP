namespace OctopusMES.Api.Services.PlmSync;

public static class ManufacturingPayloadValidator
{
    public static IReadOnlyList<ManufacturingPayloadIssue> Validate(ManufacturingPayload payload)
    {
        var issues = new List<ManufacturingPayloadIssue>();
        Required(issues, "product.productCode", payload.Product.ProductCode, "商品编码缺失");
        Required(issues, "product.productName", payload.Product.ProductName, "商品名称缺失");
        Required(issues, "product.version", payload.Product.Version, "商品版本缺失");
        Required(issues, "bomVersion", payload.BomVersion, "BOM 版本缺失");
        Required(issues, "routingVersion", payload.RoutingVersion, "工艺路线版本缺失");

        if (payload.BomItems.Count == 0) issues.Add(new("bomItems", "BOM 至少需要一项物料"));
        for (var i = 0; i < payload.BomItems.Count; i++)
        {
            var item = payload.BomItems[i];
            Required(issues, $"bomItems[{i}].materialCode", item.MaterialCode, "物料编码缺失");
            Required(issues, $"bomItems[{i}].materialName", item.MaterialName, "物料名称缺失");
            if (item.Quantity <= 0) issues.Add(new($"bomItems[{i}].quantity", "物料数量必须大于 0"));
            if (item.LossRate is < 0 or > 1) issues.Add(new($"bomItems[{i}].lossRate", "损耗率必须在 0 到 1 之间"));
        }

        if (payload.Operations.Count == 0) issues.Add(new("operations", "工艺路线至少需要一道工序"));
        for (var i = 0; i < payload.Operations.Count; i++)
        {
            var operation = payload.Operations[i];
            Required(issues, $"operations[{i}].operationCode", operation.OperationCode, "工序编码缺失");
            Required(issues, $"operations[{i}].operationName", operation.OperationName, "工序名称缺失");
            Required(issues, $"operations[{i}].requiredCapability", operation.RequiredCapability, "资源能力编码缺失");
            if (operation.StandardCycleSec <= 0) issues.Add(new($"operations[{i}].standardCycleSec", "标准工时必须大于 0 秒"));
            if (operation.SetupMinutes < 0) issues.Add(new($"operations[{i}].setupMinutes", "准备时间不能小于 0"));
        }

        return issues;
    }

    private static void Required(List<ManufacturingPayloadIssue> issues, string path, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) issues.Add(new(path, message));
    }
}
