using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OctopusUMC.Api.Services;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;

namespace OctopusUMC.Api.Hubs;

[Authorize]
public class OnlineUserHub(OnlineUserService onlineUserService, ApplicationDbContext db) : Hub
{
    public override Task OnConnectedAsync()
    {
        var userId = long.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0L;
        var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var nickName = Context.User?.FindFirst("nickname")?.Value ?? userName;
        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Resolve dept name for this user
        var deptName = string.Empty;
        if (userId > 0)
        {
            deptName = db.UserDepts
                .Where(ud => ud.UserId == userId && ud.IsPrimary)
                .Join(db.Depts, ud => ud.DeptId, d => d.DeptId, (_, d) => d.DeptName)
                .FirstOrDefault() ?? string.Empty;
        }

        onlineUserService.Add(new OnlineUser(
            Context.ConnectionId,
            userId,
            userName,
            nickName,
            deptName,
            ip,
            DateTime.UtcNow
        ));

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        onlineUserService.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
