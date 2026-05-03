using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusPLM.Api.Tests;

public class BasicFlowTests : IClassFixture<PlmTestFactory>
{
    private readonly HttpClient _client;

    public BasicFlowTests(PlmTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ShouldReturnHealthy()
    {
        var res = await _client.GetAsync("/api/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task Me_WithoutAuth_Returns401()
    {
        var res = await _client.GetAsync("/api/me");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithAuth_ReturnsUser()
    {
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        var res = await _client.GetAsync("/api/me");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("userName").GetString().Should().Be("admin");
    }

    [Fact]
    public async Task Me_WithNewUser_AutoRegistersSyncUser()
    {
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "99");

        var res = await _client.GetAsync("/api/me");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("umcUserId").GetInt32().Should().Be(99);
    }

    [Fact]
    public async Task UserSync_WithoutSignature_Returns401()
    {
        var payload = new { UserId = 1L, UserName = "admin" };

        var res = await _client.PostAsJsonAsync("/api/users/sync", payload);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserSync_WithValidSignature_Succeeds()
    {
        var payload = new { UserId = 10L, UserName = "newuser", NickName = "新用户", Email = "new@test.com", Status = 1 };
        var jsonBody = System.Text.Json.JsonSerializer.Serialize(payload);
        var secret = "octopus-sync-secret-key-2026";

        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(jsonBody))).ToLowerInvariant();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/sync")
        {
            Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Sync-Signature", $"sha256={hash}");

        var res = await _client.SendAsync(request);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task GetSyncUsers_ShouldReturnList()
    {
        var res = await _client.GetAsync("/api/users/sync");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("code").GetInt32().Should().Be(200);
        var data = json.GetProperty("data");
        data.GetArrayLength().Should().BeGreaterThan(0);
    }
}
