using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OctopusPLM.Api.Tests;

public class ProductFlowTests : IClassFixture<PlmTestFactory>
{
    private readonly HttpClient _client;

    public ProductFlowTests(PlmTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Category_Tree_ShouldReturnTree()
    {
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        var res = await _client.GetAsync("/api/category/tree");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Category_Attributes_ShouldReturnGrouped()
    {
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        // 智能手机 category has attributes (CategoryId from seed data is dynamic)
        var treeRes = await _client.GetAsync("/api/category/tree");
        var treeJson = await treeRes.Content.ReadFromJsonAsync<JsonElement>();
        var data = treeJson.GetProperty("data");

        // Get the first leaf category (smartphone, id should be around 5)
        var firstRoot = data[0];
        var firstChild = firstRoot.GetProperty("children")[0];
        long categoryId = firstChild.GetProperty("categoryId").GetInt64();

        var res = await _client.GetAsync($"/api/category/{categoryId}/attributes");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Category_Model_ShouldReturnActiveModelDetail()
    {
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        var res = await _client.GetAsync("/api/category/5/model");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("categoryId").GetInt64().Should().Be(5);
        json.GetProperty("data").GetProperty("activeVersion").GetProperty("versionNo").GetString().Should().Be("v1");
        json.GetProperty("data").GetProperty("attributeGroups").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Category_ModelVersions_ShouldReturnVersionList()
    {
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        var res = await _client.GetAsync("/api/category/5/model/versions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Product_CreateAndList_ShouldSucceed()
    {
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        // Create a product
        var createPayload = new
        {
            categoryId = 5L, // 智能手机
            productName = "测试手机",
            description = "一台测试手机",
            attributes = new Dictionary<string, string> { ["CPU 型号"] = "A18" },
            skus = new[]
            {
                new { skuCode = "PHONE-128-BLACK", price = 4999m, stock = 100, saleAttributes = new Dictionary<string, string> { ["存储容量"] = "128GB", ["内存"] = "8GB" } }
            }
        };

        var createRes = await _client.PostAsJsonAsync("/api/product", createPayload);
        createRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var createJson = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        createJson.GetProperty("code").GetInt32().Should().Be(200);
        createJson.GetProperty("data").GetProperty("productName").GetString().Should().Be("测试手机");

        long productId = createJson.GetProperty("data").GetProperty("productId").GetInt64();

        // Get the product detail
        var detailRes = await _client.GetAsync($"/api/product/{productId}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailJson = await detailRes.Content.ReadFromJsonAsync<JsonElement>();
        detailJson.GetProperty("code").GetInt32().Should().Be(200);
        detailJson.GetProperty("data").GetProperty("skus").GetArrayLength().Should().Be(1);

        // List products
        var listRes = await _client.GetAsync("/api/product/list?page=1&size=10");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await listRes.Content.ReadFromJsonAsync<JsonElement>();
        listJson.GetProperty("code").GetInt32().Should().Be(200);
        listJson.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Product_SubmitForReview_ShouldWork()
    {
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        // Create draft product
        var createPayload = new
        {
            categoryId = 5L,
            productName = "待审核手机",
            skus = new[] { new { skuCode = "TEST-001", price = 999m, stock = 10 } }
        };

        var createRes = await _client.PostAsJsonAsync("/api/product", createPayload);
        var createJson = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        long productId = createJson.GetProperty("data").GetProperty("productId").GetInt64();

        // Submit for review
        var submitRes = await _client.PutAsync($"/api/product/{productId}/submit", null);
        submitRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitJson = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        submitJson.GetProperty("code").GetInt32().Should().Be(200);

        // Verify status changed
        var detailRes = await _client.GetAsync($"/api/product/{productId}");
        var detailJson = await detailRes.Content.ReadFromJsonAsync<JsonElement>();
        detailJson.GetProperty("data").GetProperty("status").GetString().Should().Be("pending_review");
    }

    [Fact]
    public async Task Product_Category_WithoutAuth_Returns401()
    {
        var res = await _client.GetAsync("/api/category/tree");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Product_Create_WithoutAuth_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/product", new { });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
