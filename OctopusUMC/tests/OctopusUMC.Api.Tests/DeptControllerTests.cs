using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

public class DeptControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DeptControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetDeptTree_ReturnsTreeStructureWithChildren()
    {
        var response = await _client.GetAsync("/api/system/dept/tree");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DeptResponse>>>();
        result!.Code.Should().Be(200);
        result.Data.Should().NotBeEmpty();

        // Root node should have children
        var root = result.Data!.FirstOrDefault(d => d.DeptName == "章鱼科技有限公司");
        root.Should().NotBeNull();
        root!.Children.Should().NotBeEmpty("根节点应有子部门");
    }

    [Fact]
    public async Task GetDeptList_ReturnsFlatList()
    {
        var response = await _client.GetAsync("/api/system/dept/list");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DeptResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Count.Should().BeGreaterThanOrEqualTo(8, "种子数据有8个部门");
    }

    [Fact]
    public async Task GetDeptById_WithValidId_ReturnsDept()
    {
        var response = await _client.GetAsync("/api/system/dept/1");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<DeptResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.DeptName.Should().Be("章鱼科技有限公司");
    }

    [Fact]
    public async Task CreateDept_WithValidData_AppearsInTree()
    {
        var req = new { ParentId = 3, DeptName = "测试新部门_UT", OrderNum = 99, Status = 1 };
        var createResp = await _client.PostAsJsonAsync("/api/system/dept", req);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<DeptResponse>>();
        created!.Code.Should().Be(200);
        created.Data!.DeptName.Should().Be("测试新部门_UT");

        // Verify in list
        var listResp = await _client.GetAsync("/api/system/dept/list?deptName=测试新部门_UT");
        var list = await listResp.Content.ReadFromJsonAsync<ApiResponse<List<DeptResponse>>>();
        list!.Data.Should().Contain(d => d.DeptName == "测试新部门_UT");
    }

    [Fact]
    public async Task DeleteDept_WithChildren_ReturnsBadRequest()
    {
        // DeptId=1 (root) has children, should not be deletable
        var response = await _client.DeleteAsync("/api/system/dept/1");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(500, "有子部门时应返回错误");
        result.Msg.Should().Contain("子部门");
    }

    [Fact]
    public async Task DeleteDept_WithUsers_ReturnsBadRequest()
    {
        // DeptId=2 (总裁办) has admin user
        var response = await _client.DeleteAsync("/api/system/dept/2");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(500, "有用户的部门不能删除");
        result.Msg.Should().Contain("用户");
    }

    [Fact]
    public async Task UpdateDept_WithValidData_Returns200()
    {
        var req = new { DeptId = 5, ParentId = 1, DeptName = "行政部（已更新）", OrderNum = 4, Status = 1 };
        var response = await _client.PutAsJsonAsync("/api/system/dept", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<DeptResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.DeptName.Should().Be("行政部（已更新）");
    }
}
