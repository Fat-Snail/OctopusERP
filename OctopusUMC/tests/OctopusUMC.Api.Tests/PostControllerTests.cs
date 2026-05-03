using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 职位管理接口测试
/// 覆盖：CRUD 闭环、编码唯一性约束、批量删除、分页/筛选
/// </summary>
public class PostControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PostControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ─── 查询 ────────────────────────────────────────────

    [Fact]
    public async Task GetPostList_ReturnsAllSeedPosts()
    {
        var response = await _client.GetAsync("/api/system/post/list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<PostResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Total.Should().BeGreaterThanOrEqualTo(4, "种子数据有4个职位");
        result.Data.Rows.Should().Contain(p => p.PostCode == "ceo");
        result.Data.Rows.Should().Contain(p => p.PostCode == "cto");
        result.Data.Rows.Should().Contain(p => p.PostCode == "dev");
    }

    [Fact]
    public async Task GetPostList_FilterByPostName_ReturnsMatchedOnly()
    {
        var response = await _client.GetAsync("/api/system/post/list?postName=工程师");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<PostResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().OnlyContain(p => p.PostName.Contains("工程师"));
    }

    [Fact]
    public async Task GetPostList_FilterByPostCode_ReturnsMatchedOnly()
    {
        var response = await _client.GetAsync("/api/system/post/list?postCode=ceo");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<PostResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().HaveCount(1);
        result.Data.Rows[0].PostName.Should().Be("董事长");
    }

    [Fact]
    public async Task GetPostById_WithValidId_ReturnsPost()
    {
        var response = await _client.GetAsync("/api/system/post/1");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.PostCode.Should().Be("ceo");
        result.Data.PostName.Should().Be("董事长");
    }

    [Fact]
    public async Task GetPostById_WithInvalidId_Returns404()
    {
        var response = await _client.GetAsync("/api/system/post/9999");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        result!.Code.Should().Be(404);
    }

    // ─── 新增 ────────────────────────────────────────────

    [Fact]
    public async Task CreatePost_WithValidData_AppearsInList()
    {
        var req = new { PostName = "产品经理_UT", PostCode = "pm_ut", PostSort = 5, Status = 1 };

        var createResp = await _client.PostAsJsonAsync("/api/system/post", req);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        created!.Code.Should().Be(200);
        created.Data!.PostId.Should().BeGreaterThan(0);
        created.Data.PostName.Should().Be("产品经理_UT");

        // 新增后在列表中可查到（内存 CRUD 闭环）
        var listResp = await _client.GetAsync("/api/system/post/list?postCode=pm_ut");
        var list = await listResp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<PostResponse>>>();
        list!.Data!.Rows.Should().Contain(p => p.PostCode == "pm_ut");
    }

    [Fact]
    public async Task CreatePost_WithDuplicateCode_ReturnsBadRequest()
    {
        // "ceo" 已存在于种子数据
        var req = new { PostName = "另一个董事长", PostCode = "ceo", PostSort = 99, Status = 1 };
        var response = await _client.PostAsJsonAsync("/api/system/post", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        result!.Code.Should().Be(500, "职位编码已存在应返回错误");
    }

    // ─── 修改 ────────────────────────────────────────────

    [Fact]
    public async Task UpdatePost_WithValidData_ReturnsUpdatedPost()
    {
        var req = new { PostId = 2, PostName = "总经理（已更新）", PostCode = "gm", PostSort = 2, Status = 1 };
        var response = await _client.PutAsJsonAsync("/api/system/post", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.PostName.Should().Be("总经理（已更新）");
    }

    [Fact]
    public async Task UpdatePost_WithNonExistentId_Returns404()
    {
        var req = new { PostId = 9999, PostName = "幽灵职位", PostCode = "ghost", PostSort = 99, Status = 1 };
        var response = await _client.PutAsJsonAsync("/api/system/post", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        result!.Code.Should().Be(404);
    }

    // ─── 删除 ────────────────────────────────────────────

    [Fact]
    public async Task DeletePost_WithValidId_PostRemovedFromList()
    {
        // 先新增一个再删除，不影响其他测试的种子数据
        var req = new { PostName = "待删除职位_UT", PostCode = "del_ut", PostSort = 99, Status = 1 };
        var createResp = await _client.PostAsJsonAsync("/api/system/post", req);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        var newId = created!.Data!.PostId;

        var deleteResp = await _client.DeleteAsync($"/api/system/post/{newId}");
        var deleteResult = await deleteResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        deleteResult!.Code.Should().Be(200);

        // 确认已删除
        var getResp = await _client.GetAsync($"/api/system/post/{newId}");
        var getResult = await getResp.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        getResult!.Code.Should().Be(404);
    }

    [Fact]
    public async Task DeletePost_BatchByCommaIds_AllRemoved()
    {
        // 新增两个职位再批量删除
        var req1 = new { PostName = "批量删1_UT", PostCode = "batch_del1_ut", PostSort = 98, Status = 1 };
        var req2 = new { PostName = "批量删2_UT", PostCode = "batch_del2_ut", PostSort = 99, Status = 1 };

        var c1 = await (await _client.PostAsJsonAsync("/api/system/post", req1))
            .Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        var c2 = await (await _client.PostAsJsonAsync("/api/system/post", req2))
            .Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();

        var ids = $"{c1!.Data!.PostId},{c2!.Data!.PostId}";
        var deleteResp = await _client.DeleteAsync($"/api/system/post/{ids}");
        var result = await deleteResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(200);

        var get1 = await (await _client.GetAsync($"/api/system/post/{c1.Data.PostId}"))
            .Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        var get2 = await (await _client.GetAsync($"/api/system/post/{c2.Data.PostId}"))
            .Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        get1!.Code.Should().Be(404);
        get2!.Code.Should().Be(404);
    }

    [Fact]
    public async Task DeletePost_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/system/post/99999");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(404);
    }
}
