using Microsoft.AspNetCore.SignalR;
using OctopusOA.Api.Hubs;

namespace OctopusOA.Api.Services;

public class NotificationService(IHubContext<NotificationHub> hub)
{
    public async Task NotifyUserAsync(long umcUserId, string type, string message)
    {
        await hub.Clients.Group($"user_{umcUserId}")
            .SendAsync("Notification", new { type, message, at = DateTime.UtcNow });
    }

    public async Task NotifyAllAsync(string type, string message)
    {
        await hub.Clients.All.SendAsync("Notification", new { type, message, at = DateTime.UtcNow });
    }
}
