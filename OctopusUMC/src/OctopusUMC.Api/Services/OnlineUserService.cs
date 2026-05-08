using System.Collections.Concurrent;

namespace OctopusUMC.Api.Services;

public record OnlineUser(
    string ConnectionId,
    long UserId,
    string UserName,
    string NickName,
    string DeptName,
    string Ipaddr,
    DateTime LoginTime
);

public class OnlineUserService
{
    private readonly ConcurrentDictionary<string, OnlineUser> _users = new();

    public void Add(OnlineUser user) => _users[user.ConnectionId] = user;

    public void Remove(string connectionId) => _users.TryRemove(connectionId, out _);

    public IReadOnlyCollection<OnlineUser> GetAll() => _users.Values.ToList();

    public bool TryRemove(string connectionId) => _users.TryRemove(connectionId, out _);
}
