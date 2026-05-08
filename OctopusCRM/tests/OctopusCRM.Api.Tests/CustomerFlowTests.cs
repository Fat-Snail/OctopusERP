using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusCRM.Api.Tests;

public class CustomerFlowTests : IClassFixture<CrmTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public CustomerFlowTests(CrmTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetCustomerList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/customer");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var total = doc.RootElement.GetProperty("data").GetProperty("total").GetInt32();
        total.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task FilterByLevel_ReturnsOnlyMatchingCustomers()
    {
        var resp = await _client.GetAsync("/api/customer?level=A");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").GetProperty("rows");
        foreach (var row in rows.EnumerateArray())
            row.GetProperty("level").GetString().Should().Be("A");
    }

    [Fact]
    public async Task CreateCustomer_ReturnsNewCustomerWithCode()
    {
        var resp = await _client.PostAsJsonAsync("/api/customer", new
        {
            customerName = "测试新客户",
            industryType = "制造",
            level = "B",
            status = "prospect"
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("customerName").GetString().Should().Be("测试新客户");
        data.GetProperty("customerCode").GetString().Should().StartWith("CUS-");
    }

    [Fact]
    public async Task UpdateCustomer_ChangesLevel()
    {
        // 先创建
        var createResp = await _client.PostAsJsonAsync("/api/customer", new
        {
            customerName = "升级客户测试",
            level = "C",
            status = "prospect"
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var customerId = createDoc!.RootElement.GetProperty("data").GetProperty("customerId").GetInt64();

        // 再更新
        var updateResp = await _client.PutAsJsonAsync($"/api/customer/{customerId}", new
        {
            customerName = "升级客户测试",
            level = "A",
            status = "active"
        });
        var updateDoc = await updateResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        updateDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        // 验证
        var getResp = await _client.GetAsync($"/api/customer/{customerId}");
        var getDoc = await getResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        getDoc!.RootElement.GetProperty("data").GetProperty("level").GetString().Should().Be("A");
    }

    [Fact]
    public async Task DeleteCustomer_RemovesFromList()
    {
        var createResp = await _client.PostAsJsonAsync("/api/customer", new
        {
            customerName = "待删除客户",
            level = "C",
            status = "prospect"
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var customerId = createDoc!.RootElement.GetProperty("data").GetProperty("customerId").GetInt64();

        var deleteResp = await _client.DeleteAsync($"/api/customer/{customerId}");
        var deleteDoc = await deleteResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        deleteDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var getResp = await _client.GetAsync($"/api/customer/{customerId}");
        var getDoc = await getResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        getDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task GetContacts_ReturnsSeedContacts()
    {
        // 先找华为客户 ID
        var listResp = await _client.GetAsync("/api/customer?keyword=华为");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var huaweiId = listDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("customerId").GetInt64();

        var resp = await _client.GetAsync($"/api/customer/{huaweiId}/contacts");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task AddContact_ThenGetContacts_ContainsNew()
    {
        var createResp = await _client.PostAsJsonAsync("/api/customer", new
        {
            customerName = "联系人测试客户",
            level = "B",
            status = "active"
        });
        var cid = (await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("customerId").GetInt64();

        var addResp = await _client.PostAsJsonAsync($"/api/customer/{cid}/contact", new
        {
            name = "测试联系人",
            title = "测试职务",
            phone = "13900000001",
            email = "contact@test.com",
            isPrimary = true
        });
        var addDoc = await addResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        addDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        addDoc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("测试联系人");
    }

    [Fact]
    public async Task SearchByKeyword_FiltersResults()
    {
        var resp = await _client.GetAsync("/api/customer?keyword=字节");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        rows[0].GetProperty("customerName").GetString().Should().Contain("字节");
    }
}
