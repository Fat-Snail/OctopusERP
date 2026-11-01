using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusFIN.Api.Tests;

public class AccountTests : IClassFixture<FinTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public AccountTests(FinTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetAccountList_ReturnsSeedAccounts()
    {
        var resp = await _client.GetAsync("/api/account");
        resp.IsSuccessStatusCode.Should().BeTrue();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.Count.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public async Task FilterByType_ReturnsOnlyMatchingAccounts()
    {
        var resp = await _client.GetAsync("/api/account?type=asset");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.All(r => r.GetProperty("accountType").GetString() == "asset").Should().BeTrue();
        rows.Count.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task CreateAccount_DuplicateCode_Returns400()
    {
        // 1002 已是种子科目（银行存款）
        var resp = await _client.PostAsJsonAsync("/api/account", new
        {
            accountCode = "1002", accountName = "重复银行存款",
            accountType = "asset", direction = "debit", level = 1, isLeaf = true, status = "active"
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task CreateAccount_NewCode_Succeeds()
    {
        var resp = await _client.PostAsJsonAsync("/api/account", new
        {
            accountCode = $"9{Guid.NewGuid().ToString("N")[..3]}",
            accountName = "测试自定义科目",
            accountType = "asset", direction = "debit", level = 1, isLeaf = true, status = "active"
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }
}
