using FluentAssertions;
using OctopusPLM.Api.Services;
using OctopusPLM.Core.Entities;

namespace OctopusPLM.Api.Tests;

public class ManufacturingReleaseValidatorTests
{
    [Fact]
    public void Validate_ReturnsFieldPathsForIncompleteManufacturingData()
    {
        var version = new ManufacturingVersion
        {
            BomItems =
            [
                new ManufacturingBomItem
                {
                    MaterialCode = "",
                    MaterialName = "",
                    Quantity = 0,
                    LossRate = -1
                }
            ],
            Operations =
            [
                new ManufacturingOperation
                {
                    OperationCode = "",
                    OperationName = "",
                    RequiredCapability = "",
                    StandardCycleSec = 0,
                    SetupMinutes = -1
                }
            ]
        };

        var paths = ManufacturingReleaseValidator.Validate(version).Select(x => x.Path);

        paths.Should().BeEquivalentTo(
            "bomItems[0].materialId",
            "bomItems[0].materialCode",
            "bomItems[0].materialName",
            "bomItems[0].quantity",
            "bomItems[0].lossRate",
            "operations[0].operationCode",
            "operations[0].operationName",
            "operations[0].requiredCapability",
            "operations[0].standardCycleSec",
            "operations[0].setupMinutes");
    }

    [Fact]
    public void Validate_ReturnsCollectionErrorsWhenBomAndRoutingAreEmpty()
    {
        var issues = ManufacturingReleaseValidator.Validate(new ManufacturingVersion());

        issues.Select(x => x.Path).Should().BeEquivalentTo("bomItems", "operations");
    }

    [Fact]
    public void Validate_ReturnsNoErrorsForCompleteManufacturingData()
    {
        var version = new ManufacturingVersion
        {
            BomItems =
            [
                new ManufacturingBomItem
                {
                    MaterialId = 1,
                    MaterialCode = "MAT-001",
                    MaterialName = "真实物料",
                    Quantity = 1,
                    LossRate = 0
                }
            ],
            Operations =
            [
                new ManufacturingOperation
                {
                    OperationCode = "OP-001",
                    OperationName = "真实工序",
                    RequiredCapability = "CAP-001",
                    StandardCycleSec = 60,
                    SetupMinutes = 0
                }
            ]
        };

        ManufacturingReleaseValidator.Validate(version).Should().BeEmpty();
    }
}
