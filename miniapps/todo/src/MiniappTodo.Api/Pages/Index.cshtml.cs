using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MiniappTodo.Api.Persistence;
using MiniappTodo.Api.Services;

namespace MiniappTodo.Api.Pages;

public class IndexModel(TodoDbContext db, UserContextService ctx) : PageModel
{
    public bool IsAuthed { get; set; }
    public string? UserName { get; set; }
    public string? DeptName { get; set; }
    public string? CompanyName { get; set; }
    public string? Scope { get; set; }
    public string? OctopusUserId { get; set; }
    public int Total { get; set; }
    public int Done { get; set; }
    public int Pending { get; set; }
    public int Overdue { get; set; }

    public async Task OnGetAsync()
    {
        IsAuthed = User.Identity?.IsAuthenticated == true;
        if (!IsAuthed) return;

        var info = ctx.Extract(User);
        UserName = info.Name ?? info.Username;
        DeptName = info.PrimaryDeptName;
        CompanyName = info.CompanyName;
        Scope = info.MiniappScope;
        OctopusUserId = info.OctopusUserId;

        await ctx.EnsureProfileAsync(User);

        var sub = info.Sub;
        Total = await db.Todos.CountAsync(t => t.KcSub == sub);
        Done = await db.Todos.CountAsync(t => t.KcSub == sub && t.IsDone);
        Pending = Total - Done;
        Overdue = await db.Todos.CountAsync(t => t.KcSub == sub && !t.IsDone
            && t.DueDate.HasValue && t.DueDate < DateTime.UtcNow);
    }
}
