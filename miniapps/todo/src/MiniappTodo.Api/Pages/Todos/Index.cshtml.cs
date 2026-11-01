using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MiniappTodo.Api.Persistence;
using MiniappTodo.Api.Services;

namespace MiniappTodo.Api.Pages.Todos;

[Authorize]
public class IndexModel(TodoDbContext db, UserContextService ctx) : PageModel
{
    public List<TodoItem> Items { get; set; } = new();
    public string? FilterMode { get; set; }   // all / pending / done

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public int Priority { get; set; } = 3;
    }

    public async Task OnGetAsync(string? mode)
    {
        FilterMode = mode ?? "all";
        var sub = ctx.RequireSub(User);
        await ctx.EnsureProfileAsync(User);

        var q = db.Todos.Where(t => t.KcSub == sub);
        if (FilterMode == "pending") q = q.Where(t => !t.IsDone);
        else if (FilterMode == "done") q = q.Where(t => t.IsDone);

        Items = await q
            .OrderBy(t => t.IsDone)
            .ThenByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Title))
        {
            TempData["Message"] = "标题不能为空";
            return RedirectToPage();
        }

        var sub = ctx.RequireSub(User);
        await ctx.EnsureProfileAsync(User);

        var now = DateTime.UtcNow;
        db.Todos.Add(new TodoItem
        {
            KcSub = sub,
            Title = Input.Title.Trim(),
            Description = Input.Description,
            DueDate = Input.DueDate,
            Priority = Input.Priority is >= 1 and <= 5 ? Input.Priority : 3,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        TempData["Message"] = $"已创建：{Input.Title}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(long id)
    {
        var sub = ctx.RequireSub(User);
        var t = await db.Todos.FirstOrDefaultAsync(x => x.TodoId == id && x.KcSub == sub);
        if (t is null) return NotFound();

        var wasDone = t.IsDone;
        t.IsDone = !wasDone;
        t.CompletedAt = !wasDone ? DateTime.UtcNow : null;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Message"] = t.IsDone ? $"已完成：{t.Title}" : $"已重置：{t.Title}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        var sub = ctx.RequireSub(User);
        var t = await db.Todos.FirstOrDefaultAsync(x => x.TodoId == id && x.KcSub == sub);
        if (t is null) return NotFound();
        db.Todos.Remove(t);
        await db.SaveChangesAsync();
        TempData["Message"] = $"已删除：{t.Title}";
        return RedirectToPage();
    }
}
