using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;

namespace OctopusUMC.Api.Controllers;

/// <summary>文件管理接口（本阶段为内存模拟，无真实上传）</summary>
[ApiController]
[Route("api/system/oss")]
public class FileController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public FileController(ApplicationDbContext context) => _context = context;

    private OssFileResponse MapFile(OssFile f) => new()
    {
        OssId = f.OssId,
        FileName = f.FileName,
        OriginalName = f.OriginalName,
        FileSuffix = f.FileSuffix,
        Url = f.Url,
        Service = f.Service,
        CreateBy = f.CreateBy,
        CreateTime = f.CreateTime,
    };

    /// <summary>分页查询文件列表</summary>
    [HttpGet("list")]
    public ApiResponse<PagedResult<OssFileResponse>> GetList(
        [FromQuery] string? fileName,
        [FromQuery] string? fileSuffix,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.OssFiles.AsQueryable();
        if (!string.IsNullOrEmpty(fileName))
            query = query.Where(f => f.OriginalName.Contains(fileName));
        if (!string.IsNullOrEmpty(fileSuffix))
            query = query.Where(f => f.FileSuffix == fileSuffix);

        var total = query.Count();
        var rows = query.OrderByDescending(f => f.CreateTime)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .ToList()
            .Select(MapFile).ToList();
        return ApiResponse<PagedResult<OssFileResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>根据文件ID获取详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<OssFileResponse> GetById(long id)
    {
        var f = _context.OssFiles.FirstOrDefault(f => f.OssId == id);
        if (f == null) return ApiResponse<OssFileResponse>.Fail("文件不存在", 404);
        return ApiResponse<OssFileResponse>.Success(MapFile(f));
    }

    /// <summary>模拟上传文件（仅注册文件记录，不涉及真实文件）</summary>
    [HttpPost("upload")]
    public ApiResponse<OssFileResponse> Upload([FromBody] MockUploadRequest req)
    {
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "admin";
        var file = new OssFile
        {
            FileName = req.FileName,
            OriginalName = req.OriginalName,
            FileSuffix = req.FileSuffix,
            Url = $"/uploads/{req.FileName}",
            Service = "local",
            CreateBy = userName,
            CreateTime = DateTime.UtcNow,
        };
        _context.OssFiles.Add(file);
        _context.SaveChanges();
        return ApiResponse<OssFileResponse>.Success(MapFile(file), "上传成功");
    }

    /// <summary>批量删除文件记录（逗号分隔ID）</summary>
    [HttpDelete("{ids}")]
    public ApiResponse<object?> Delete(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.OssFiles.Where(f => idList.Contains(f.OssId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("文件不存在", 404);
        _context.OssFiles.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }
}

/// <summary>模拟上传请求（Step2 不涉及真实文件传输）</summary>
public class MockUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string FileSuffix { get; set; } = string.Empty;
}
