using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusPLM.Api.Tests;

public class ManufacturingReleaseApiTests : IClassFixture<PlmTestFactory>
{
    private readonly HttpClient _client;

    public ManufacturingReleaseApiTests(PlmTestFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");
    }

    [Fact]
    public async Task IncompleteDraft_ExposesIssuesAndCannotBeReleased()
    {
        var versionName = $"TEST-{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/manufacturing/versions", new
        {
            productId = 1,
            version = versionName,
            bomVersion = "BOM-TEST",
            routingVersion = "ROUTE-TEST",
            bomItems = new[]
            {
                new { lineNo = 10, materialCode = "", materialName = "", quantity = 1, lossRate = 0 }
            },
            operations = new[]
            {
                new { sequence = 10, operationCode = "", operationName = "", requiredCapability = "", standardCycleSec = 60, setupMinutes = 0, isSplittable = true }
            }
        });
        var created = await create.Content.ReadFromJsonAsync<JsonDocument>();
        var id = created!.RootElement.GetProperty("data").GetProperty("manufacturingVersionId").GetInt64();

        var detailResponse = await _client.GetAsync($"/api/manufacturing/versions/{id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var detailData = detail!.RootElement.GetProperty("data");
        detailData.GetProperty("releaseReady").GetBoolean().Should().BeFalse();
        detailData.GetProperty("validationIssues").EnumerateArray()
            .Select(x => x.GetProperty("path").GetString())
            .Should().Contain("operations[0].requiredCapability");

        var releaseResponse = await _client.PostAsync($"/api/manufacturing/versions/{id}/release", null);
        var release = await releaseResponse.Content.ReadFromJsonAsync<JsonDocument>();
        release!.RootElement.GetProperty("code").GetInt32().Should().Be(422);
        release.RootElement.GetProperty("data").GetProperty("validationIssues").GetArrayLength().Should().BeGreaterThan(0);

        var afterResponse = await _client.GetAsync($"/api/manufacturing/versions/{id}");
        var after = await afterResponse.Content.ReadFromJsonAsync<JsonDocument>();
        after!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("draft");
    }
}
