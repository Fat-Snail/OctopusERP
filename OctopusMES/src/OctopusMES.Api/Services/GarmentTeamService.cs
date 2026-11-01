using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public class GarmentTeamService(MesDbContext db)
{
    public async Task<object> ListAsync(string? keyword, string? status, bool? includeInactive)
    {
        var q = db.GarmentTeams.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(t => t.Status == status);
        else if (includeInactive != true)
            q = q.Where(t => t.Status == "active");
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(t =>
                t.TeamCode.Contains(keyword) ||
                t.TeamName.Contains(keyword) ||
                t.GroupLabel.Contains(keyword) ||
                t.Leader.Contains(keyword) ||
                (t.OutsourcePartnerName ?? "").Contains(keyword));

        var rows = await q.OrderBy(t => t.SortOrder).ToListAsync();
        return new { rows, total = rows.Count };
    }

    public async Task<MesGarmentTeam?> GetAsync(long id) => await db.GarmentTeams.FindAsync(id);

    public async Task<(MesGarmentTeam? Team, string? Error)> CreateAsync(MesGarmentTeam req)
    {
        if (string.IsNullOrWhiteSpace(req.TeamCode)) return (null, "TeamCode 必填");
        if (string.IsNullOrWhiteSpace(req.TeamName)) return (null, "TeamName 必填");
        if (await db.GarmentTeams.AnyAsync(t => t.TeamCode == req.TeamCode))
            return (null, $"TeamCode '{req.TeamCode}' 已存在");
        if (req.IsOutsource && string.IsNullOrWhiteSpace(req.OutsourcePartnerName))
            return (null, "外协组必须填写 OutsourcePartnerName");
        if (!req.IsOutsource && req.LeaderUmcUserId == null)
            return (null, "自有组必须指定 LeaderUmcUserId");

        // SortOrder 自动取最大值+1
        if (req.SortOrder == 0)
        {
            var max = await db.GarmentTeams.MaxAsync(t => (int?)t.SortOrder) ?? 0;
            req.SortOrder = max + 1;
        }
        if (string.IsNullOrWhiteSpace(req.GroupLabel)) req.GroupLabel = req.TeamName;
        if (string.IsNullOrWhiteSpace(req.Status)) req.Status = "active";
        req.CreatedAt = DateTime.UtcNow;

        // 外协组的 Leader 字段冗余存合作方名,自有组保持组长名快照
        if (req.IsOutsource && string.IsNullOrWhiteSpace(req.Leader))
            req.Leader = req.OutsourcePartnerName ?? "";

        db.GarmentTeams.Add(req);
        await db.SaveChangesAsync();
        return (req, null);
    }

    public async Task<(MesGarmentTeam? Team, string? Error)> UpdateAsync(MesGarmentTeam req)
    {
        var t = await db.GarmentTeams.FindAsync(req.TeamId);
        if (t == null) return (null, "组不存在");

        // TeamCode 不可改(避免破坏种子映射)
        t.TeamName = req.TeamName;
        t.GroupLabel = req.GroupLabel;
        t.Leader = req.Leader;
        t.LeaderUmcUserId = req.LeaderUmcUserId;
        t.UmcDeptId = req.UmcDeptId;
        t.IsOutsource = req.IsOutsource;
        t.OutsourcePartnerName = req.OutsourcePartnerName;
        t.Headcount = req.Headcount;
        t.Status = string.IsNullOrWhiteSpace(req.Status) ? t.Status : req.Status;
        t.SortOrder = req.SortOrder == 0 ? t.SortOrder : req.SortOrder;
        t.Remark = req.Remark;

        await db.SaveChangesAsync();
        return (t, null);
    }

    public async Task<(MesGarmentTeam? Team, string? Error)> SetStatusAsync(long id, string status)
    {
        var t = await db.GarmentTeams.FindAsync(id);
        if (t == null) return (null, "组不存在");
        if (status != "active" && status != "inactive")
            return (t, "status 必须为 active 或 inactive");
        t.Status = status;
        await db.SaveChangesAsync();
        return (t, null);
    }

    /// <summary>
    /// 候选组长：返回 UMC 中"车间组长"职位的用户(通过 mes_sync_user 缓存)。
    /// 注意：MES 通过 SyncUser 表缓存 UMC 用户。这里返回所有 active 同步用户，前端可再筛。
    /// </summary>
    public async Task<object> GetCandidateLeadersAsync()
    {
        var users = await db.SyncUsers
            .Where(u => u.Status == 1)
            .OrderBy(u => u.UmcUserId)
            .Select(u => new { u.UmcUserId, u.UserName, u.NickName, u.Email })
            .ToListAsync();

        // 过滤掉已经分配为组长的（避免一人多组冲突）
        var occupiedLeaderIds = await db.GarmentTeams
            .Where(t => t.LeaderUmcUserId != null && t.Status == "active")
            .Select(t => t.LeaderUmcUserId!.Value)
            .ToListAsync();

        var occupiedSet = occupiedLeaderIds.ToHashSet();
        var rows = users.Select(u => new
        {
            u.UmcUserId,
            u.UserName,
            u.NickName,
            u.Email,
            isOccupied = occupiedSet.Contains(u.UmcUserId),
        }).ToList();

        return new { rows, total = rows.Count };
    }
}
