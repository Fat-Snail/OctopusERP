using OctopusPLM.Core.Entities;

namespace OctopusPLM.Api.Services;

public sealed record ManufacturingValidationIssue(string Path, string Message);

public static class ManufacturingReleaseValidator
{
    public static IReadOnlyList<ManufacturingValidationIssue> Validate(ManufacturingVersion version)
    {
        var issues = new List<ManufacturingValidationIssue>();

        if (version.BomItems.Count == 0)
            issues.Add(new("bomItems", "至少需要一条 BOM 明细"));

        for (var index = 0; index < version.BomItems.Count; index++)
        {
            var item = version.BomItems[index];
            var prefix = $"bomItems[{index}]";
            if (!item.MaterialId.HasValue)
                issues.Add(new($"{prefix}.materialId", "待匹配物料"));
            if (string.IsNullOrWhiteSpace(item.MaterialCode))
                issues.Add(new($"{prefix}.materialCode", "物料编码"));
            if (string.IsNullOrWhiteSpace(item.MaterialName))
                issues.Add(new($"{prefix}.materialName", "物料名称"));
            if (item.Quantity <= 0)
                issues.Add(new($"{prefix}.quantity", "用量必须大于 0"));
            if (item.LossRate < 0)
                issues.Add(new($"{prefix}.lossRate", "损耗率不能小于 0"));
        }

        if (version.Operations.Count == 0)
            issues.Add(new("operations", "至少需要一道工序"));

        for (var index = 0; index < version.Operations.Count; index++)
        {
            var operation = version.Operations[index];
            var prefix = $"operations[{index}]";
            if (string.IsNullOrWhiteSpace(operation.OperationCode))
                issues.Add(new($"{prefix}.operationCode", "工序编码"));
            if (string.IsNullOrWhiteSpace(operation.OperationName))
                issues.Add(new($"{prefix}.operationName", "工序名称"));
            if (string.IsNullOrWhiteSpace(operation.RequiredCapability))
                issues.Add(new($"{prefix}.requiredCapability", "能力编码"));
            if (operation.StandardCycleSec <= 0)
                issues.Add(new($"{prefix}.standardCycleSec", "标准工时必须大于 0"));
            if (operation.SetupMinutes < 0)
                issues.Add(new($"{prefix}.setupMinutes", "换型时间不能小于 0"));
        }

        return issues;
    }
}
