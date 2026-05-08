using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>Phase 5：操作日志 AOP + 在线用户 SignalR REST 端点测试</summary>
public class Phase5MonitorTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public Phase5MonitorTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── 操作日志 AOP ─────────────────────────────────────────────────

    [Fact]
    public async Task OperLog_ListEndpoint_Returns200WithSeedData()
    {
        var res = await _client.GetAsync("/api/monitor/operlog/list");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await res.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OperLogResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Total.Should().BeGreaterThanOrEqualTo(5); // 5 seed OperLogs
        result.Data.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OperLog_AfterLoggedMutation_RecordsNewEntry()
    {
        // Get current count
        var before = await _client.GetAsync("/api/monitor/operlog/list");
        var beforeResult = await before.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OperLogResponse>>>();
        var countBefore = beforeResult!.Data!.Total;

        // Call a [Log]-decorated endpoint: POST /api/system/user
        var createResp = await _client.PostAsJsonAsync("/api/system/user", new
        {
            UserName = $"aoptest_{Guid.NewGuid():N}"[..16],
            NickName = "AOP测试",
            Email = $"aop_{Guid.NewGuid():N}@test.com",
            Password = "Test@123",
            DeptIds = new[] { 2 },
            PostIds = Array.Empty<int>(),
            RoleIds = Array.Empty<int>(),
            Status = 1
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Count should have increased by 1
        var after = await _client.GetAsync("/api/monitor/operlog/list?pageSize=100");
        var afterResult = await after.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OperLogResponse>>>();
        afterResult!.Data!.Total.Should().Be(countBefore + 1);

        // The new log should have the correct title
        afterResult.Data.Rows.Should().Contain(l => l.Title == "用户管理-新增");
    }

    [Fact]
    public async Task OperLog_UnloggedGetEndpoint_DoesNotAddEntry()
    {
        var before = await _client.GetAsync("/api/monitor/operlog/list");
        var countBefore = (await before.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OperLogResponse>>>())!
            .Data!.Total;

        // GET endpoint has no [Log] attribute
        await _client.GetAsync("/api/system/user/list");

        var after = await _client.GetAsync("/api/monitor/operlog/list");
        var countAfter = (await after.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OperLogResponse>>>())!
            .Data!.Total;

        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public async Task OperLog_DeleteEndpoint_Returns200()
    {
        // Seed has OperId=1..5; delete one
        var res = await _client.DeleteAsync("/api/monitor/operlog/1");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await res.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        result!.Code.Should().Be(200);

        // Confirm deletion
        var list = await _client.GetAsync("/api/monitor/operlog/list?pageSize=100");
        var listResult = await list.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OperLogResponse>>>();
        listResult!.Data!.Rows.Should().NotContain(l => l.OperId == 1);
    }

    // ── 在线用户（SignalR REST 端点）────────────────────────────────

    [Fact]
    public async Task OnlineUsers_ListEndpoint_Returns200WithEmptyList()
    {
        // No SignalR connections in test env → list is always empty
        var res = await _client.GetAsync("/api/monitor/online/list");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await res.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OnlineUserResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().BeEmpty();
        result.Data.Total.Should().Be(0);
    }

    [Fact]
    public async Task OnlineUsers_ForceLogout_WithNonExistentId_Returns404()
    {
        var res = await _client.DeleteAsync("/api/monitor/online/nonexistent-connection-id");
        // TryRemove returns false → 404
        var result = await res.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        result!.Code.Should().Be(404);
    }
}
