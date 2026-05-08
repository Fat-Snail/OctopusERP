using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusPLM.Api.Tests;

/// <summary>商品审核流程（提交审核 → 通过/驳回 → 上架 → 下架）集成测试</summary>
public class ReviewFlowTests : IClassFixture<PlmTestFactory>
{
    private readonly HttpClient _client;

    public ReviewFlowTests(PlmTestFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");
    }

    private async Task<long> CreateDraftProductAsync(string name = "审核测试商品")
    {
        var payload = new
        {
            categoryId = 5L,
            productName = name,
            skus = new[] { new { skuCode = $"SKU-{Guid.NewGuid().ToString("N")[..6]}", price = 999m, stock = 10 } }
        };
        var res = await _client.PostAsJsonAsync("/api/product", payload);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").GetProperty("productId").GetInt64();
    }

    [Fact]
    public async Task FullReviewFlow_DraftToActive_ShouldSucceed()
    {
        // 1. Create draft
        var productId = await CreateDraftProductAsync("全流程测试商品");

        // 2. Submit for review → pending_review
        var submitRes = await _client.PutAsync($"/api/product/{productId}/submit", null);
        submitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        (await submitRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetInt32().Should().Be(200);

        var afterSubmit = await _client.GetAsync($"/api/product/{productId}");
        var afterSubmitJson = await afterSubmit.Content.ReadFromJsonAsync<JsonElement>();
        afterSubmitJson.GetProperty("data").GetProperty("status").GetString().Should().Be("pending_review");

        // 3. Approve → approved
        var approveRes = await _client.PutAsJsonAsync($"/api/product/{productId}/approve", new { comment = "外观合规" });
        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        (await approveRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetInt32().Should().Be(200);

        var afterApprove = await _client.GetAsync($"/api/product/{productId}");
        var afterApproveJson = await afterApprove.Content.ReadFromJsonAsync<JsonElement>();
        afterApproveJson.GetProperty("data").GetProperty("status").GetString().Should().Be("approved");

        // 4. Publish → active
        var publishRes = await _client.PutAsync($"/api/product/{productId}/publish", null);
        publishRes.StatusCode.Should().Be(HttpStatusCode.OK);
        (await publishRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetInt32().Should().Be(200);

        var afterPublish = await _client.GetAsync($"/api/product/{productId}");
        var afterPublishJson = await afterPublish.Content.ReadFromJsonAsync<JsonElement>();
        afterPublishJson.GetProperty("data").GetProperty("status").GetString().Should().Be("active");

        // 5. Discontinue → discontinued
        var discontinueRes = await _client.PutAsJsonAsync($"/api/product/{productId}/discontinue", new { comment = "促销结束" });
        discontinueRes.StatusCode.Should().Be(HttpStatusCode.OK);
        (await discontinueRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetInt32().Should().Be(200);

        var afterDiscontinue = await _client.GetAsync($"/api/product/{productId}");
        var afterDiscontinueJson = await afterDiscontinue.Content.ReadFromJsonAsync<JsonElement>();
        afterDiscontinueJson.GetProperty("data").GetProperty("status").GetString().Should().Be("discontinued");
    }

    [Fact]
    public async Task Reject_PendingReview_ShouldChangeToRejected()
    {
        var productId = await CreateDraftProductAsync("驳回测试商品");
        await _client.PutAsync($"/api/product/{productId}/submit", null);

        var rejectRes = await _client.PutAsJsonAsync($"/api/product/{productId}/reject", new { comment = "资料不完整" });
        rejectRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailJson = await (await _client.GetAsync($"/api/product/{productId}")).Content.ReadFromJsonAsync<JsonElement>();
        detailJson.GetProperty("data").GetProperty("status").GetString().Should().Be("rejected");
    }

    [Fact]
    public async Task ReviewHistory_ShouldRecordAllActions()
    {
        var productId = await CreateDraftProductAsync("历史记录测试商品");

        // submit → approve → publish
        await _client.PutAsync($"/api/product/{productId}/submit", null);
        await _client.PutAsJsonAsync($"/api/product/{productId}/approve", new { comment = "ok" });
        await _client.PutAsync($"/api/product/{productId}/publish", null);

        var historyRes = await _client.GetAsync($"/api/product/{productId}/reviews");
        historyRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyJson = await historyRes.Content.ReadFromJsonAsync<JsonElement>();
        historyJson.GetProperty("code").GetInt32().Should().Be(200);
        historyJson.GetProperty("data").GetArrayLength().Should().Be(3);

        var first = historyJson.GetProperty("data")[0];
        first.GetProperty("action").GetString().Should().Be("submit");
    }

    [Fact]
    public async Task ProductStats_ShouldIncludeAllStatuses()
    {
        var statsRes = await _client.GetAsync("/api/product/stats");
        statsRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var statsJson = await statsRes.Content.ReadFromJsonAsync<JsonElement>();
        statsJson.GetProperty("code").GetInt32().Should().Be(200);
        var data = statsJson.GetProperty("data");
        data.TryGetProperty("total", out _).Should().BeTrue();
        data.TryGetProperty("draft", out _).Should().BeTrue();
        data.TryGetProperty("pendingReview", out _).Should().BeTrue();
        data.TryGetProperty("approved", out _).Should().BeTrue();
        data.TryGetProperty("active", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Approve_Draft_ShouldFail()
    {
        var productId = await CreateDraftProductAsync("非法审核测试");
        // Try to approve without submitting first
        var approveRes = await _client.PutAsJsonAsync($"/api/product/{productId}/approve", new { });
        var approveJson = await approveRes.Content.ReadFromJsonAsync<JsonElement>();
        approveJson.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task Category_CRUD_ShouldWork()
    {
        // Create root category
        var createRes = await _client.PostAsJsonAsync("/api/category", new { name = "测试根类目", orderNum = 100 });
        createRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var createJson = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        createJson.GetProperty("code").GetInt32().Should().Be(200);
        long catId = createJson.GetProperty("data").GetProperty("categoryId").GetInt64();

        // Update
        var updateRes = await _client.PutAsJsonAsync($"/api/category/{catId}", new { name = "更新后类目", orderNum = 50 });
        (await updateRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetInt32().Should().Be(200);

        // Delete
        var deleteRes = await _client.DeleteAsync($"/api/category/{catId}");
        (await deleteRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task Attribute_CRUD_ShouldWork()
    {
        // Create
        var createRes = await _client.PostAsJsonAsync("/api/attribute", new
        {
            name = "测试属性",
            code = $"test_attr_{Guid.NewGuid().ToString("N")[..6]}",
            attributeType = "text",
            inputType = "single_line",
            valueScope = "global"
        });
        var createJson = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        createJson.GetProperty("code").GetInt32().Should().Be(200);
        long attrId = createJson.GetProperty("data").GetProperty("attributeId").GetInt64();

        // Update
        var updateRes = await _client.PutAsJsonAsync($"/api/attribute/{attrId}", new { name = "更新属性", unit = "kg" });
        (await updateRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetInt32().Should().Be(200);

        // Delete
        var deleteRes = await _client.DeleteAsync($"/api/attribute/{attrId}");
        (await deleteRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetInt32().Should().Be(200);
    }
}
