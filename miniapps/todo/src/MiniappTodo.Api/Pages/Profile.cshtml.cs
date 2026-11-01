using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MiniappTodo.Api.Pages;

[Authorize]
public class ProfileModel : PageModel
{
    public List<Claim> Claims { get; set; } = new();

    public void OnGet()
    {
        Claims = User.Claims
            .OrderBy(c => c.Type)
            .ToList();
    }
}
