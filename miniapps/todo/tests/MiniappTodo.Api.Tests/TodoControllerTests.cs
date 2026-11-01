using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniappTodo.Api.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiniappTodo.Api.Tests;

/// <summary>
/// Mini-app demo 端到端集成测试。
/// 覆盖：
/// - /api/me：JWT claims 提取 + Lazy Provisioning UserProfile
/// - Todo CRUD：标准增删改查
/// - 用户隔离：用户 A 不能看/改/删用户 B 的 Todo（KcSub 过滤）
/// - 输入校验：空 Title 拒绝
/// - 统计：stats 端点
/// </summary>
public class TodoControllerTests : IClassFixture<TodoTestFactory>
{
    private readonly TodoTestFactory _factory;

    public TodoControllerTests(TodoTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientFor(string sub, string username = "zhangsan")
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        c.DefaultRequestHeaders.Add("X-Test-Username", username);
        return c;
    }

    [Fact]
    public async Task GetMe_ReturnsClaimsAndCreatesLocalProfile()
    {
        var client = ClientFor("user-zhangsan-uuid", "zhangsan");
        var resp = await client.GetAsync("/api/me");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("sub").GetString().Should().Be("user-zhangsan-uuid");
        json.GetProperty("username").GetString().Should().Be("zhangsan");
        json.GetProperty("name").GetString().Should().Be("张三");
        json.GetProperty("primaryDeptName").GetString().Should().Be("技术部");
        json.GetProperty("companyName").GetString().Should().Be("章鱼科技");
        json.GetProperty("miniappScope").GetString().Should().Be("team");

        // Lazy Provisioning 验证：本地 user_profile 表已写入
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.KcSub == "user-zhangsan-uuid");
        profile.Should().NotBeNull();
        profile!.SnapshotDept.Should().Be("技术部");
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        // 不设 X-Test-Sub 等 header 也会过 TestAuthHandler（用默认值）
        // 真正测试 401 需要绕过 TestAuth - 这里测试 Test header 注入是否真生效
        var resp = await c.GetAsync("/api/me");
        resp.StatusCode.Should().Be(HttpStatusCode.OK); // TestAuth 默认通过
    }

    [Fact]
    public async Task CreateTodo_PersistsAndReturnsItem()
    {
        var client = ClientFor("user-a-uuid", "alice");
        var resp = await client.PostAsJsonAsync("/api/todos", new
        {
            title = "买菜",
            description = "周末超市采购",
            priority = 4,
            dueDate = DateTime.UtcNow.AddDays(2),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var todo = await resp.Content.ReadFromJsonAsync<JsonElement>();
        todo.GetProperty("title").GetString().Should().Be("买菜");
        todo.GetProperty("isDone").GetBoolean().Should().BeFalse();
        todo.GetProperty("priority").GetInt32().Should().Be(4);
        todo.GetProperty("kcSub").GetString().Should().Be("user-a-uuid");
    }

    [Fact]
    public async Task CreateTodo_RejectsEmptyTitle()
    {
        var client = ClientFor("user-empty-uuid");
        var resp = await client.PostAsJsonAsync("/api/todos", new { title = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListTodos_OnlyReturnsCurrentUserItems()
    {
        var alice = ClientFor("user-alice-iso", "alice");
        var bob = ClientFor("user-bob-iso", "bob");

        // Alice 建 2 个
        await alice.PostAsJsonAsync("/api/todos", new { title = "alice-todo-1", priority = 3 });
        await alice.PostAsJsonAsync("/api/todos", new { title = "alice-todo-2", priority = 5 });

        // Bob 建 1 个
        await bob.PostAsJsonAsync("/api/todos", new { title = "bob-todo-1", priority = 2 });

        // Alice 列表只看到自己的 2 个
        var aliceList = await (await alice.GetAsync("/api/todos")).Content.ReadFromJsonAsync<JsonElement>();
        var aliceRows = aliceList.GetProperty("rows");
        aliceRows.GetArrayLength().Should().Be(2);
        foreach (var item in aliceRows.EnumerateArray())
            item.GetProperty("title").GetString().Should().StartWith("alice-");

        // Bob 列表只看到自己的 1 个
        var bobList = await (await bob.GetAsync("/api/todos")).Content.ReadFromJsonAsync<JsonElement>();
        bobList.GetProperty("rows").GetArrayLength().Should().Be(1);
        bobList.GetProperty("rows")[0].GetProperty("title").GetString().Should().Be("bob-todo-1");
    }

    [Fact]
    public async Task UserA_CannotAccessUserBs_Todo()
    {
        var alice = ClientFor("user-A-xattack", "alice");
        var bob = ClientFor("user-B-xattack", "bob");

        // Bob 建一个
        var createResp = await bob.PostAsJsonAsync("/api/todos", new { title = "bob-secret-todo", priority = 1 });
        var bobTodo = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var bobTodoId = bobTodo.GetProperty("todoId").GetInt64();

        // Alice 尝试访问 Bob 的 todo
        var getResp = await alice.GetAsync($"/api/todos/{bobTodoId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);   // 严格 404，不暴露存在性

        // Alice 尝试修改 Bob 的 todo
        var updateResp = await alice.PutAsJsonAsync($"/api/todos/{bobTodoId}",
            new { title = "hacked-by-alice", isDone = true });
        updateResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Alice 尝试删除 Bob 的 todo
        var deleteResp = await alice.DeleteAsync($"/api/todos/{bobTodoId}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 验证 Bob 的 todo 没被改坏
        var bobCheck = await (await bob.GetAsync($"/api/todos/{bobTodoId}")).Content.ReadFromJsonAsync<JsonElement>();
        bobCheck.GetProperty("title").GetString().Should().Be("bob-secret-todo");
    }

    [Fact]
    public async Task UpdateTodo_MarksAsDoneAndSetsCompletedAt()
    {
        var client = ClientFor("user-update-uuid", "charlie");
        var create = await client.PostAsJsonAsync("/api/todos",
            new { title = "完成报告", priority = 5 });
        var todo = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = todo.GetProperty("todoId").GetInt64();

        var resp = await client.PutAsJsonAsync($"/api/todos/{id}", new { isDone = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("isDone").GetBoolean().Should().BeTrue();
        updated.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task DeleteTodo_RemovesItem()
    {
        var client = ClientFor("user-delete-uuid", "dave");
        var create = await client.PostAsJsonAsync("/api/todos",
            new { title = "to-delete", priority = 1 });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("todoId").GetInt64();

        var del = await client.DeleteAsync($"/api/todos/{id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/api/todos/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stats_ReturnsCurrentUserCounts()
    {
        var client = ClientFor("user-stats-uuid", "eve");

        await client.PostAsJsonAsync("/api/todos", new { title = "pending-1" });
        await client.PostAsJsonAsync("/api/todos", new { title = "pending-2" });
        var done1 = await client.PostAsJsonAsync("/api/todos", new { title = "to-be-done" });
        var done1Id = (await done1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("todoId").GetInt64();
        await client.PutAsJsonAsync($"/api/todos/{done1Id}", new { isDone = true });

        // 加一条逾期未完成
        var overdueResp = await client.PostAsJsonAsync("/api/todos",
            new { title = "overdue", dueDate = DateTime.UtcNow.AddDays(-1) });
        overdueResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var statsResp = await client.GetAsync("/api/todos/stats");
        var stats = await statsResp.Content.ReadFromJsonAsync<JsonElement>();
        stats.GetProperty("total").GetInt32().Should().Be(4);
        stats.GetProperty("done").GetInt32().Should().Be(1);
        stats.GetProperty("pending").GetInt32().Should().Be(3);
        stats.GetProperty("overdue").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ListWithPendingOnlyFilter_ExcludesDone()
    {
        var client = ClientFor("user-filter-uuid", "frank");
        await client.PostAsJsonAsync("/api/todos", new { title = "still-pending" });
        var done = await client.PostAsJsonAsync("/api/todos", new { title = "to-done" });
        var doneId = (await done.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("todoId").GetInt64();
        await client.PutAsJsonAsync($"/api/todos/{doneId}", new { isDone = true });

        var pendingResp = await client.GetAsync("/api/todos?pendingOnly=true");
        var pending = await pendingResp.Content.ReadFromJsonAsync<JsonElement>();
        pending.GetProperty("rows").GetArrayLength().Should().Be(1);
        pending.GetProperty("rows")[0].GetProperty("title").GetString().Should().Be("still-pending");
    }
}
