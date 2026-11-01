using FluentAssertions;
using OctopusMES.Api.Services.PlmSync;

namespace OctopusMES.Api.Tests;

public sealed class ManufacturingPayloadValidationTests
{
    [Fact]
    public void Validate_ReturnsStablePathsForIncompletePayload()
    {
        var payload = ValidPayload() with
        {
            Product = new(2, "", "", "V1.0"),
            BomItems = [new(10, "", "", 0, -1)],
            Operations = [new(10, "", "", "", 0, -1, true)],
        };

        var paths = ManufacturingPayloadValidator.Validate(payload).Select(x => x.Path);

        paths.Should().Contain([
            "product.productCode", "product.productName", "bomItems[0].materialCode",
            "bomItems[0].materialName", "bomItems[0].quantity", "bomItems[0].lossRate",
            "operations[0].operationCode", "operations[0].operationName",
            "operations[0].requiredCapability", "operations[0].standardCycleSec",
            "operations[0].setupMinutes",
        ]);
    }

    [Fact]
    public void Validate_RejectsEmptyBomAndRouting()
    {
        var payload = ValidPayload() with { BomItems = [], Operations = [] };

        ManufacturingPayloadValidator.Validate(payload).Select(x => x.Path)
            .Should().Contain(["bomItems", "operations"]);
    }

    [Fact]
    public void ComputeHash_IsStableWhenCollectionsArriveInDifferentOrder()
    {
        var payload = ValidPayload() with
        {
            BomItems = [new(20, "BAT", "电池", 1, 0), new(10, "SCR", "屏幕", 1, 0)],
            Operations = [new(20, "TEST", "测试", "PHONE_TEST", 30, 0, false), new(10, "ASSY", "装配", "PHONE_ASSY", 60, 2, true)],
        };
        var reordered = payload with
        {
            BomItems = payload.BomItems.AsEnumerable().Reverse().ToList(),
            Operations = payload.Operations.AsEnumerable().Reverse().ToList(),
        };

        ManufacturingPayloadHasher.ComputeHash(payload).Should().Be(ManufacturingPayloadHasher.ComputeHash(reordered));
    }

    [Fact]
    public void MatchesDeclaredHash_RejectsDifferentContent()
    {
        var payload = ValidPayload();

        ManufacturingPayloadHasher.MatchesDeclaredHash(payload with { PayloadHash = new string('f', 64) })
            .Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsCompletePayload()
    {
        ManufacturingPayloadValidator.Validate(ValidPayload()).Should().BeEmpty();
    }

    private static ManufacturingPayload ValidPayload() => new(
        new(2, "PHONE-001", "手机", "V1.0"),
        3,
        "BOM-V1.0",
        "ROUTE-V1.0",
        "",
        [new(10, "SCR", "屏幕", 1, 0)],
        [new(10, "ASSY", "装配", "PHONE_ASSY", 60, 2, true)]);
}
