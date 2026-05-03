using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

public class UserControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetUserList_Returns200WithPagedData()
    {
        var response = await _client.GetAsync("/api/system/user/list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        result!.Code.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.Total.Should().BeGreaterThan(0);
        result.Data.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUserList_WithStatusFilter_ReturnsFilteredData()
    {
        // Status=0 should return disabled users (wangwu)
        var response = await _client.GetAsync("/api/system/user/list?status=0");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().OnlyContain(u => u.Status == 0);
    }

    [Fact]
    public async Task GetUserById_WithValidId_ReturnsUser()
    {
        var response = await _client.GetAsync("/api/system/user/1");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.UserName.Should().Be("admin");
        result.Data.DeptName.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUserById_WithInvalidId_Returns404()
    {
        var response = await _client.GetAsync("/api/system/user/9999");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Code.Should().Be(404);
    }

    [Fact]
    public async Task CreateUser_WithValidData_Returns200AndUserAppearsInList()
    {
        var createReq = new
        {
            UserName = "testuser_ut",
            NickName = "测试用户",
            Email = "testuser_ut@octopus.com",
            Sex = "1",
            Password = "Test@123",
            DeptIds = new[] { 3 },
            PostIds = new[] { 4 },
            RoleIds = new[] { 2 },
            Status = 1
        };

        var createResp = await _client.PostAsJsonAsync("/api/system/user", createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        created!.Code.Should().Be(200);
        created.Data!.UserName.Should().Be("testuser_ut");

        // Verify appears in list
        var listResp = await _client.GetAsync("/api/system/user/list?userName=testuser_ut");
        var list = await listResp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        list!.Data!.Rows.Should().Contain(u => u.UserName == "testuser_ut");
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUserName_Returns500()
    {
        var createReq = new
        {
            UserName = "admin", // already exists
            NickName = "另一个管理员",
            Email = "admin2@octopus.com",
            Password = "Admin@123",
            DeptIds = new[] { 2 },
            Status = 1
        };
        var response = await _client.PostAsJsonAsync("/api/system/user", createReq);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Code.Should().Be(500);
    }

    [Fact]
    public async Task UpdateUser_WithValidData_Returns200()
    {
        var updateReq = new
        {
            UserId = 2,
            UserName = "zhangsan",
            NickName = "张三（已更新）",
            Email = "zhangsan_new@octopus.com",
            Sex = "1",
            DeptIds = new[] { 3 },
            PostIds = new[] { 3 },
            RoleIds = new[] { 2 },
            Status = 1
        };

        var response = await _client.PutAsJsonAsync("/api/system/user", updateReq);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.NickName.Should().Be("张三（已更新）");
    }

    [Fact]
    public async Task DeleteUser_WithValidIds_Returns200AndUserRemovedFromList()
    {
        // First create a user to delete
        var createReq = new
        {
            UserName = "delete_me_test",
            NickName = "待删除用户",
            Email = "deleteme@octopus.com",
            Password = "Delete@123",
            DeptIds = new[] { 3 },
            Status = 1
        };
        var createResp = await _client.PostAsJsonAsync("/api/system/user", createReq);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        var newId = created!.Data!.UserId;

        // Delete the user
        var deleteResp = await _client.DeleteAsync($"/api/system/user/{newId}");
        var deleteResult = await deleteResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        deleteResult!.Code.Should().Be(200);

        // Verify user is gone
        var getResp = await _client.GetAsync($"/api/system/user/{newId}");
        var getResult = await getResp.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        getResult!.Code.Should().Be(404);
    }

    [Fact]
    public async Task ResetPassword_Returns200()
    {
        var req = new { UserId = 2, NewPassword = "NewPass@123" };
        var response = await _client.PutAsJsonAsync("/api/system/user/resetPwd", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task UpdateStatus_DisableUser_Returns200()
    {
        var req = new { UserId = 2, Status = 0 };
        var response = await _client.PutAsJsonAsync("/api/system/user/status", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(200);
    }
}
