using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusPLM.Api.Tests;

public sealed class MaterialApiTests : IClassFixture<PlmTestFactory>
{
    private readonly HttpClient client;

    public MaterialApiTests(PlmTestFactory factory)
    {
        client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");
    }

    [Fact]
    public async Task CreateNormalizesCodeAndSearchesByCodeOrName()
    {
        var code = $"mat-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/materials", new
        {
            materialCode = code, materialName = "手机屏幕组件", specification = "6.36 AMOLED",
            baseUnit = "片", materialType = "purchased_part", status = "active"
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonDocument>();
        created!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        created.RootElement.GetProperty("data").GetProperty("materialCode").GetString().Should().Be(code.ToUpperInvariant());

        var search = await client.GetFromJsonAsync<JsonDocument>($"/api/materials?keyword={Uri.EscapeDataString("屏幕组件")}&page=1&pageSize=20");
        search!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray()
            .Should().Contain(x => x.GetProperty("materialCode").GetString() == code.ToUpperInvariant());

        var duplicate = await client.PostAsJsonAsync("/api/materials", new
        {
            materialCode = code.ToUpperInvariant(), materialName = "重复", baseUnit = "片",
            materialType = "purchased_part", status = "active"
        });
        var duplicateBody = await duplicate.Content.ReadFromJsonAsync<JsonDocument>();
        duplicateBody!.RootElement.GetProperty("code").GetInt32().Should().Be(409);
    }
}
