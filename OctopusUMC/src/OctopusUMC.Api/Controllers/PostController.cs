using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.Attributes;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>职位管理接口</summary>
[ApiController]
[Route("api/system/post")]
public class PostController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public PostController(ApplicationDbContext context) => _context = context;

    private PostResponse MapPost(Post p) => new()
    {
        PostId = p.PostId,
        PostName = p.PostName,
        PostCode = p.PostCode,
        PostSort = p.PostSort,
        Status = p.Status,
        CreateTime = p.CreateTime,
        Remark = p.Remark,
    };

    /// <summary>分页查询职位列表</summary>
    [HttpGet("list")]
    public ApiResponse<PagedResult<PostResponse>> GetList(
        [FromQuery] string? postName,
        [FromQuery] string? postCode,
        [FromQuery] int? status,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.Posts.AsQueryable();
        if (!string.IsNullOrEmpty(postName))
            query = query.Where(p => p.PostName.Contains(postName));
        if (!string.IsNullOrEmpty(postCode))
            query = query.Where(p => p.PostCode.Contains(postCode));
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var total = query.Count();
        var rows = query.OrderBy(p => p.PostSort)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .ToList()
            .Select(MapPost).ToList();
        return ApiResponse<PagedResult<PostResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>根据职位ID获取详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<PostResponse> GetById(long id)
    {
        var p = _context.Posts.FirstOrDefault(p => p.PostId == id);
        if (p == null) return ApiResponse<PostResponse>.Fail("职位不存在", 404);
        return ApiResponse<PostResponse>.Success(MapPost(p));
    }

    /// <summary>新增职位</summary>
    [Log("职位管理-新增")]
    [HttpPost]
    public ApiResponse<PostResponse> Create([FromBody] CreatePostRequest req)
    {
        if (_context.Posts.Any(p => p.PostCode == req.PostCode))
            return ApiResponse<PostResponse>.Fail("职位编码已存在");

        var post = new Post
        {
            PostName = req.PostName,
            PostCode = req.PostCode,
            PostSort = req.PostSort,
            Status = req.Status,
            Remark = req.Remark,
            CreateTime = DateTime.UtcNow,
        };
        _context.Posts.Add(post);
        _context.SaveChanges();
        return ApiResponse<PostResponse>.Success(MapPost(post), "新增成功");
    }

    /// <summary>修改职位</summary>
    [Log("职位管理-修改")]
    [HttpPut]
    public ApiResponse<PostResponse> Update([FromBody] UpdatePostRequest req)
    {
        var p = _context.Posts.FirstOrDefault(p => p.PostId == req.PostId);
        if (p == null) return ApiResponse<PostResponse>.Fail("职位不存在", 404);
        p.PostName = req.PostName;
        p.PostCode = req.PostCode;
        p.PostSort = req.PostSort;
        p.Status = req.Status;
        p.Remark = req.Remark;
        _context.SaveChanges();
        return ApiResponse<PostResponse>.Success(MapPost(p), "修改成功");
    }

    /// <summary>批量删除职位（逗号分隔ID）</summary>
    [Log("职位管理-删除")]
    [HttpDelete("{ids}")]
    public ApiResponse<object?> Delete(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.Posts.Where(p => idList.Contains(p.PostId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("职位不存在", 404);
        _context.Posts.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }
}
