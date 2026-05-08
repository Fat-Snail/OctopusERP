using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Services;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>任务调度管理接口</summary>
[ApiController]
[Route("api/monitor/job")]
public class JobController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly JobSchedulerService _scheduler;
    public JobController(ApplicationDbContext context, JobSchedulerService scheduler)
    {
        _context = context;
        _scheduler = scheduler;
    }

    private JobResponse MapJob(Job j) => new()
    {
        JobId = j.JobId,
        JobName = j.JobName,
        JobGroup = j.JobGroup,
        InvokeTarget = j.InvokeTarget,
        CronExpression = j.CronExpression,
        Status = j.Status,
        CreateTime = j.CreateTime,
        Remark = j.Remark,
    };

    /// <summary>分页查询任务列表</summary>
    [HttpGet("list")]
    public ApiResponse<PagedResult<JobResponse>> GetList(
        [FromQuery] string? jobName,
        [FromQuery] string? jobGroup,
        [FromQuery] int? status,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.Jobs.AsQueryable();
        if (!string.IsNullOrEmpty(jobName))
            query = query.Where(j => j.JobName.Contains(jobName));
        if (!string.IsNullOrEmpty(jobGroup))
            query = query.Where(j => j.JobGroup == jobGroup);
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        var total = query.Count();
        var rows = query.Skip((pageNum - 1) * pageSize).Take(pageSize).ToList().Select(MapJob).ToList();
        return ApiResponse<PagedResult<JobResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>根据任务ID获取详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<JobResponse> GetById(long id)
    {
        var j = _context.Jobs.FirstOrDefault(j => j.JobId == id);
        if (j == null) return ApiResponse<JobResponse>.Fail("任务不存在", 404);
        return ApiResponse<JobResponse>.Success(MapJob(j));
    }

    /// <summary>新增任务</summary>
    [HttpPost]
    public ApiResponse<JobResponse> Create([FromBody] CreateJobRequest req)
    {
        var job = new Job
        {
            JobName = req.JobName,
            JobGroup = req.JobGroup,
            InvokeTarget = req.InvokeTarget,
            CronExpression = req.CronExpression,
            Status = req.Status,
            Remark = req.Remark,
            CreateTime = DateTime.UtcNow,
        };
        _context.Jobs.Add(job);
        _context.SaveChanges();
        return ApiResponse<JobResponse>.Success(MapJob(job), "新增成功");
    }

    /// <summary>修改任务</summary>
    [HttpPut]
    public ApiResponse<JobResponse> Update([FromBody] UpdateJobRequest req)
    {
        var j = _context.Jobs.FirstOrDefault(j => j.JobId == req.JobId);
        if (j == null) return ApiResponse<JobResponse>.Fail("任务不存在", 404);
        j.JobName = req.JobName;
        j.JobGroup = req.JobGroup;
        j.InvokeTarget = req.InvokeTarget;
        j.CronExpression = req.CronExpression;
        j.Status = req.Status;
        j.Remark = req.Remark;
        _context.SaveChanges();
        return ApiResponse<JobResponse>.Success(MapJob(j), "修改成功");
    }

    /// <summary>批量删除任务（逗号分隔ID）</summary>
    [HttpDelete("{ids}")]
    public ApiResponse<object?> Delete(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.Jobs.Where(j => idList.Contains(j.JobId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("任务不存在", 404);
        _context.Jobs.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }

    /// <summary>修改任务状态（启用/停用）</summary>
    [HttpPut("status")]
    public ApiResponse<object?> UpdateStatus([FromBody] UpdateStatusRequest req)
    {
        var j = _context.Jobs.FirstOrDefault(j => j.JobId == req.UserId);
        if (j == null) return ApiResponse<object?>.Fail("任务不存在", 404);
        j.Status = req.Status;
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "状态更新成功");
    }

    /// <summary>立即触发一次任务</summary>
    [HttpPut("run/{id:long}")]
    public async Task<ApiResponse<object?>> Run(long id)
    {
        var j = _context.Jobs.FirstOrDefault(j => j.JobId == id);
        if (j == null) return ApiResponse<object?>.Fail("任务不存在", 404);
        await _scheduler.ExecuteJobAsync(_context, j, DateTime.UtcNow, CancellationToken.None);
        return ApiResponse<object?>.Success(null, "任务已触发");
    }

    /// <summary>暂停任务</summary>
    [HttpPut("pause/{id:long}")]
    public ApiResponse<object?> Pause(long id)
    {
        var j = _context.Jobs.FirstOrDefault(j => j.JobId == id);
        if (j == null) return ApiResponse<object?>.Fail("任务不存在", 404);
        j.Status = 0;
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "任务已暂停");
    }

    /// <summary>恢复任务</summary>
    [HttpPut("resume/{id:long}")]
    public ApiResponse<object?> Resume(long id)
    {
        var j = _context.Jobs.FirstOrDefault(j => j.JobId == id);
        if (j == null) return ApiResponse<object?>.Fail("任务不存在", 404);
        j.Status = 1;
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "任务已恢复");
    }

    /// <summary>查询任务执行日志</summary>
    [HttpGet("log/list")]
    public ApiResponse<PagedResult<JobLogResponse>> GetLogs(
        [FromQuery] long? jobId,
        [FromQuery] int? status,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.JobLogs.AsQueryable();
        if (jobId.HasValue) query = query.Where(l => l.JobId == jobId.Value);
        if (status.HasValue) query = query.Where(l => l.Status == status.Value);

        var total = query.Count();
        var rows = query.OrderByDescending(l => l.StartTime)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .ToList()
            .Select(l => new JobLogResponse
            {
                JobLogId = l.JobLogId,
                JobId = l.JobId,
                JobName = l.JobName,
                JobGroup = l.JobGroup,
                InvokeTarget = l.InvokeTarget,
                StartTime = l.StartTime,
                ElapsedMs = l.ElapsedMs,
                Status = l.Status,
                ErrorMsg = l.ErrorMsg,
            }).ToList();
        return ApiResponse<PagedResult<JobLogResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>清空任务日志</summary>
    [HttpDelete("log/clean")]
    public ApiResponse<object?> CleanLogs()
    {
        _context.JobLogs.RemoveRange(_context.JobLogs.ToList());
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "日志已清空");
    }
}
