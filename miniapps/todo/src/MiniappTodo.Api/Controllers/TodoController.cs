using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniappTodo.Api.Persistence;
using MiniappTodo.Api.Services;

namespace MiniappTodo.Api.Controllers;

/// <summary>
/// Todo 业务：标准 CRUD。所有查询/写入按 KcSub 严格隔离。
/// 一个用户只能看到/修改自己的 Todo。
///
/// 认证：通过 MultiAuthScheme PolicyScheme 自动识别
///   - Authorization: Bearer xxx 头 → JwtBearer
///   - 否则 → Cookie（同源浏览器）
/// </summary>
[ApiController]
[Authorize]
[Route("api/todos")]
public class TodoController(TodoDbContext db, UserContextService ctx) : ControllerBase
{
    public record CreateRequest(string Title, string? Description, DateTime? DueDate, int? Priority);
    public record UpdateRequest(string? Title, string? Description, DateTime? DueDate, int? Priority, bool? IsDone);

    /// <summary>列表（仅当前用户的）</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? doneOnly, [FromQuery] bool? pendingOnly,
        CancellationToken ct)
    {
        var sub = ctx.RequireSub(User);
        await ctx.EnsureProfileAsync(User, ct);

        var q = db.Todos.Where(t => t.KcSub == sub);
        if (doneOnly == true) q = q.Where(t => t.IsDone);
        else if (pendingOnly == true) q = q.Where(t => !t.IsDone);

        var rows = await q
            .OrderBy(t => t.IsDone)
            .ThenByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.TodoId, t.Title, t.Description, t.IsDone, t.DueDate,
                t.Priority, t.CreatedAt, t.UpdatedAt, t.CompletedAt
            })
            .ToListAsync(ct);

        return Ok(new { rows, total = rows.Count });
    }

    /// <summary>详情（仅当前用户的）</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var sub = ctx.RequireSub(User);
        var t = await db.Todos.FirstOrDefaultAsync(x => x.TodoId == id && x.KcSub == sub, ct);
        return t is null ? NotFound() : Ok(t);
    }

    /// <summary>新建</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { msg = "Title 必填" });

        var sub = ctx.RequireSub(User);
        await ctx.EnsureProfileAsync(User, ct);

        var now = DateTime.UtcNow;
        var todo = new TodoItem
        {
            KcSub = sub,
            Title = req.Title.Trim(),
            Description = req.Description,
            DueDate = req.DueDate,
            Priority = req.Priority is >= 1 and <= 5 ? req.Priority.Value : 3,
            IsDone = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Todos.Add(todo);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = todo.TodoId }, todo);
    }

    /// <summary>更新（仅当前用户的）</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRequest req, CancellationToken ct)
    {
        var sub = ctx.RequireSub(User);
        var t = await db.Todos.FirstOrDefaultAsync(x => x.TodoId == id && x.KcSub == sub, ct);
        if (t is null) return NotFound();

        if (req.Title is not null) t.Title = req.Title;
        if (req.Description is not null) t.Description = req.Description;
        if (req.DueDate is not null) t.DueDate = req.DueDate;
        if (req.Priority is >= 1 and <= 5) t.Priority = req.Priority.Value;
        if (req.IsDone is not null)
        {
            var wasDone = t.IsDone;
            t.IsDone = req.IsDone.Value;
            t.CompletedAt = !wasDone && t.IsDone ? DateTime.UtcNow : null;
        }
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(t);
    }

    /// <summary>删除（仅当前用户的）</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var sub = ctx.RequireSub(User);
        var t = await db.Todos.FirstOrDefaultAsync(x => x.TodoId == id && x.KcSub == sub, ct);
        if (t is null) return NotFound();
        db.Todos.Remove(t);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>当前用户的统计快照</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var sub = ctx.RequireSub(User);
        var total = await db.Todos.CountAsync(t => t.KcSub == sub, ct);
        var done = await db.Todos.CountAsync(t => t.KcSub == sub && t.IsDone, ct);
        var overdue = await db.Todos.CountAsync(t =>
            t.KcSub == sub && !t.IsDone && t.DueDate.HasValue && t.DueDate < DateTime.UtcNow, ct);
        return Ok(new { total, done, pending = total - done, overdue });
    }
}
