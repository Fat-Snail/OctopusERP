using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

/// <summary>通讯录查询服务</summary>
public class ContactService
{
    private readonly OaDbContext _db;

    public ContactService(OaDbContext db) => _db = db;

    /// <summary>获取部门树（带每个节点的用户数）</summary>
    public List<ContactDeptNode> GetDeptTree()
    {
        var allDepts = _db.OaDepts.Where(d => d.Status == 1).ToList();
        var nodes = allDepts.Select(d => new ContactDeptNode
        {
            DeptId = d.DeptId,
            ParentId = d.ParentId,
            DeptName = d.DeptName,
            OrderNum = d.OrderNum,
            UserCount = CountUsersInDeptAndChildren(d.DeptId, allDepts),
        }).ToList();

        return BuildTree(nodes, 0);
    }

    private int CountUsersInDeptAndChildren(long deptId, List<OaDept> allDepts)
    {
        var deptIds = CollectChildDeptIds(deptId, allDepts);
        deptIds.Add(deptId);
        return _db.OaUserDepts.Where(ud => deptIds.Contains(ud.DeptId))
            .Select(ud => ud.UmcUserId).Distinct().Count();
    }

    private List<long> CollectChildDeptIds(long parentId, List<OaDept> allDepts)
    {
        var result = new List<long>();
        var children = allDepts.Where(d => d.ParentId == parentId).Select(d => d.DeptId).ToList();
        foreach (var childId in children)
        {
            result.Add(childId);
            result.AddRange(CollectChildDeptIds(childId, allDepts));
        }
        return result;
    }

    private List<ContactDeptNode> BuildTree(List<ContactDeptNode> all, long parentId) =>
        all.Where(n => n.ParentId == parentId)
           .OrderBy(n => n.OrderNum)
           .Select(n => { n.Children = BuildTree(all, n.DeptId); return n; })
           .ToList();

    /// <summary>查询员工（支持部门筛选 + 关键字搜索）</summary>
    public List<ContactUser> SearchUsers(long? deptId, string? keyword)
    {
        var activeUsers = _db.SyncUsers.Where(u => u.Status == 1).ToList();
        var userIds = activeUsers.Select(u => u.UmcUserId).ToHashSet();

        if (deptId.HasValue)
        {
            var allDepts = _db.OaDepts.ToList();
            var targetDeptIds = CollectChildDeptIds(deptId.Value, allDepts);
            targetDeptIds.Add(deptId.Value);
            var userIdsInDept = _db.OaUserDepts
                .Where(ud => targetDeptIds.Contains(ud.DeptId))
                .Select(ud => ud.UmcUserId).ToList().ToHashSet();
            userIds.IntersectWith(userIdsInDept);
        }

        var users = activeUsers.Where(u => userIds.Contains(u.UmcUserId)).ToList();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            users = users.Where(u =>
                u.UserName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                u.NickName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (u.Email?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (u.PhoneNumber?.Contains(kw) ?? false)
            ).ToList();
        }

        return users.Select(MapUser).OrderBy(u => u.UmcUserId).ToList();
    }

    /// <summary>用户详情</summary>
    public ContactUser? GetUser(long umcUserId)
    {
        var user = _db.SyncUsers.FirstOrDefault(u => u.UmcUserId == umcUserId);
        return user == null ? null : MapUser(user);
    }

    private ContactUser MapUser(SyncUser u)
    {
        var userDepts = _db.OaUserDepts.Where(ud => ud.UmcUserId == u.UmcUserId).ToList();
        var depts = userDepts.Select(ud =>
        {
            var dept = _db.OaDepts.FirstOrDefault(d => d.DeptId == ud.DeptId);
            return new ContactDeptInfo
            {
                DeptId = ud.DeptId,
                DeptName = dept?.DeptName ?? "未知部门",
                PostName = ud.PostName,
                IsPrimary = ud.IsPrimary,
            };
        }).OrderByDescending(d => d.IsPrimary).ToList();

        return new ContactUser
        {
            UmcUserId = u.UmcUserId,
            UserName = u.UserName,
            NickName = u.NickName,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            Avatar = u.Avatar,
            Status = u.Status,
            Depts = depts,
        };
    }

    /// <summary>处理 UMC 部门同步</summary>
    public void SyncDept(DeptSyncPayload payload)
    {
        var existing = _db.OaDepts.FirstOrDefault(d => d.DeptId == payload.DeptId);

        if (payload.Action == "delete")
        {
            if (existing != null)
            {
                _db.OaDepts.Remove(existing);
                _db.SaveChanges();
            }
            return;
        }

        if (existing != null)
        {
            existing.ParentId = payload.ParentId;
            existing.DeptName = payload.DeptName;
            existing.OrderNum = payload.OrderNum;
            existing.Status = payload.Status;
            existing.LastSyncAt = DateTime.UtcNow;
        }
        else
        {
            _db.OaDepts.Add(new OaDept
            {
                DeptId = payload.DeptId,
                ParentId = payload.ParentId,
                DeptName = payload.DeptName,
                OrderNum = payload.OrderNum,
                Status = payload.Status,
                LastSyncAt = DateTime.UtcNow,
            });
        }
        _db.SaveChanges();
    }
}
