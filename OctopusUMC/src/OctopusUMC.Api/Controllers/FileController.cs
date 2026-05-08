using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;

namespace OctopusUMC.Api.Controllers;

/// <summary>文件管理接口（本地磁盘存储）</summary>
[ApiController]
[Route("api/system/oss")]
public class FileController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public FileController(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private string UploadsDir => Path.Combine(_env.ContentRootPath, "uploads");

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

    /// <summary>上传文件（multipart/form-data）</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<ApiResponse<OssFileResponse>> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResponse<OssFileResponse>.Fail("请选择文件");

        Directory.CreateDirectory(UploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var savePath = Path.Combine(UploadsDir, storedName);

        await using (var stream = System.IO.File.Create(savePath))
            await file.CopyToAsync(stream);

        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "anonymous";
        var record = new OssFile
        {
            FileName = storedName,
            OriginalName = file.FileName,
            FileSuffix = ext,
            Url = $"/api/system/oss/download/{storedName}",
            Service = "local",
            CreateBy = userName,
            CreateTime = DateTime.UtcNow,
        };
        _context.OssFiles.Add(record);
        _context.SaveChanges();

        return ApiResponse<OssFileResponse>.Success(MapFile(record), "上传成功");
    }

    /// <summary>通用上传入口（同 /upload，兼容前端 /common/upload 路径）</summary>
    [HttpPost("/api/common/upload")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public Task<ApiResponse<OssFileResponse>> CommonUpload(IFormFile file) => Upload(file);

    /// <summary>下载文件（通过存储文件名）</summary>
    [HttpGet("download/{storedName}")]
    public IActionResult Download(string storedName)
    {
        // 阻止路径穿越
        var safeName = Path.GetFileName(storedName);
        var filePath = Path.Combine(UploadsDir, safeName);
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { code = 404, msg = "文件不存在" });

        var record = _context.OssFiles.FirstOrDefault(f => f.FileName == safeName);
        var contentType = GetContentType(Path.GetExtension(safeName));
        return PhysicalFile(filePath, contentType, record?.OriginalName ?? safeName);
    }

    /// <summary>批量删除文件记录（逗号分隔ID，同时删除磁盘文件）</summary>
    [HttpDelete("{ids}")]
    public ApiResponse<object?> Delete(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.OssFiles.Where(f => idList.Contains(f.OssId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("文件不存在", 404);

        foreach (var item in items)
        {
            var path = Path.Combine(UploadsDir, item.FileName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        _context.OssFiles.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }

    private static string GetContentType(string ext) => ext.ToLower() switch
    {
        ".png"  => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif"  => "image/gif",
        ".pdf"  => "application/pdf",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls"  => "application/vnd.ms-excel",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".zip"  => "application/zip",
        _       => "application/octet-stream",
    };
}
