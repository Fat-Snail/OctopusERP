using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Services;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;

namespace OctopusUMC.Api.Controllers;

/// <summary>公告管理接口</summary>
[ApiController]
[Route("api/system/notice")]
public class NoticeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly NoticeSyncService _syncService;
    public NoticeController(ApplicationDbContext context, NoticeSyncService syncService)
    {
        _context = context;
        _syncService = syncService;
    }

    private void PushSync(Notice n, string action = "upsert")
    {
        _ = _syncService.NotifyNoticeChangedAsync(new NoticeSyncPayload
        {
            Action = action,
            NoticeId = n.NoticeId,
            Title = n.NoticeTitle,
            Content = n.NoticeContent,
            NoticeType = n.NoticeType,
            Priority = n.NoticeType == "3" ? 10 : (n.NoticeType == "2" ? 5 : 0),
            Publisher = n.CreateBy ?? "admin",
            PublishTime = n.CreateTime,
            Status = n.Status,
        });
    }

    private NoticeResponse MapNotice(Notice n) => new()
    {
        NoticeId = n.NoticeId,
        NoticeTitle = n.NoticeTitle,
        NoticeType = n.NoticeType,
        NoticeContent = n.NoticeContent,
        Status = n.Status,
        CreateBy = n.CreateBy,
        CreateTime = n.CreateTime,
        Remark = n.Remark,
    };

    /// <summary>分页查询公告列表</summary>
    [HttpGet("list")]
    public ApiResponse<PagedResult<NoticeResponse>> GetList(
        [FromQuery] string? noticeTitle,
        [FromQuery] string? noticeType,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.Notices.AsQueryable();
        if (!string.IsNullOrEmpty(noticeTitle))
            query = query.Where(n => n.NoticeTitle.Contains(noticeTitle));
        if (!string.IsNullOrEmpty(noticeType))
            query = query.Where(n => n.NoticeType == noticeType);

        var total = query.Count();
        var rows = query.OrderByDescending(n => n.CreateTime)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .ToList()
            .Select(MapNotice).ToList();
        return ApiResponse<PagedResult<NoticeResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>根据公告ID获取详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<NoticeResponse> GetById(long id)
    {
        var n = _context.Notices.FirstOrDefault(n => n.NoticeId == id);
        if (n == null) return ApiResponse<NoticeResponse>.Fail("公告不存在", 404);
        return ApiResponse<NoticeResponse>.Success(MapNotice(n));
    }

    /// <summary>新增公告</summary>
    [HttpPost]
    public ApiResponse<NoticeResponse> Create([FromBody] CreateNoticeRequest req)
    {
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "admin";
        var notice = new Notice
        {
            NoticeTitle = req.NoticeTitle,
            NoticeType = req.NoticeType,
            NoticeContent = req.NoticeContent,
            Status = req.Status,
            Remark = req.Remark,
            CreateBy = userName,
            CreateTime = DateTime.UtcNow,
        };
        _context.Notices.Add(notice);
        _context.SaveChanges();
        PushSync(notice);
        return ApiResponse<NoticeResponse>.Success(MapNotice(notice), "新增成功");
    }

    /// <summary>修改公告</summary>
    [HttpPut]
    public ApiResponse<NoticeResponse> Update([FromBody] UpdateNoticeRequest req)
    {
        var n = _context.Notices.FirstOrDefault(n => n.NoticeId == req.NoticeId);
        if (n == null) return ApiResponse<NoticeResponse>.Fail("公告不存在", 404);
        n.NoticeTitle = req.NoticeTitle;
        n.NoticeType = req.NoticeType;
        n.NoticeContent = req.NoticeContent;
        n.Status = req.Status;
        n.Remark = req.Remark;
        _context.SaveChanges();
        PushSync(n);
        return ApiResponse<NoticeResponse>.Success(MapNotice(n), "修改成功");
    }

    /// <summary>批量删除公告（逗号分隔ID）</summary>
    [HttpDelete("{ids}")]
    public ApiResponse<object?> Delete(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.Notices.Where(n => idList.Contains(n.NoticeId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("公告不存在", 404);
        _context.Notices.RemoveRange(items);
        _context.SaveChanges();
        foreach (var n in items) PushSync(n, "delete");
        return ApiResponse<object?>.Success(null, "删除成功");
    }
}
