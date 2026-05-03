using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>OIDC 接入应用管理接口</summary>
[ApiController]
[Route("api/tool/client")]
public class OidcClientController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public OidcClientController(ApplicationDbContext context) => _context = context;

    private OidcClientResponse MapClient(OidcClient c) => new()
    {
        Id = c.Id,
        ClientId = c.ClientId,
        ClientName = c.ClientName,
        ClientType = c.ClientType,
        RedirectUris = c.RedirectUris,
        PostLogoutRedirectUris = c.PostLogoutRedirectUris,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
    };

    /// <summary>分页查询接入应用列表</summary>
    [HttpGet("list")]
    public ApiResponse<PagedResult<OidcClientResponse>> GetList(
        [FromQuery] string? clientName,
        [FromQuery] int? status,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.OidcClients.AsQueryable();
        if (!string.IsNullOrEmpty(clientName))
            query = query.Where(c => c.ClientName.Contains(clientName));
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var total = query.Count();
        var rows = query.Skip((pageNum - 1) * pageSize).Take(pageSize).ToList().Select(MapClient).ToList();
        return ApiResponse<PagedResult<OidcClientResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>根据应用ID获取详情</summary>
    [HttpGet("{id}")]
    public ApiResponse<OidcClientResponse> GetById(string id)
    {
        var c = _context.OidcClients.FirstOrDefault(c => c.Id == id);
        if (c == null) return ApiResponse<OidcClientResponse>.Fail("应用不存在", 404);
        return ApiResponse<OidcClientResponse>.Success(MapClient(c));
    }

    /// <summary>新增接入应用</summary>
    [HttpPost]
    public ApiResponse<OidcClientResponse> Create([FromBody] CreateOidcClientRequest req)
    {
        if (_context.OidcClients.Any(c => c.ClientId == req.ClientId))
            return ApiResponse<OidcClientResponse>.Fail("ClientId 已存在");

        var client = new OidcClient
        {
            Id = Guid.NewGuid().ToString(),
            ClientId = req.ClientId,
            ClientName = req.ClientName,
            ClientType = req.ClientType,
            RedirectUris = req.RedirectUris,
            PostLogoutRedirectUris = req.PostLogoutRedirectUris,
            Status = req.Status,
            CreatedAt = DateTime.UtcNow,
        };
        _context.OidcClients.Add(client);
        _context.SaveChanges();
        return ApiResponse<OidcClientResponse>.Success(MapClient(client), "新增成功");
    }

    /// <summary>修改接入应用</summary>
    [HttpPut]
    public ApiResponse<OidcClientResponse> Update([FromBody] UpdateOidcClientRequest req)
    {
        var c = _context.OidcClients.FirstOrDefault(c => c.Id == req.Id);
        if (c == null) return ApiResponse<OidcClientResponse>.Fail("应用不存在", 404);
        c.ClientId = req.ClientId;
        c.ClientName = req.ClientName;
        c.ClientType = req.ClientType;
        c.RedirectUris = req.RedirectUris;
        c.PostLogoutRedirectUris = req.PostLogoutRedirectUris;
        c.Status = req.Status;
        _context.SaveChanges();
        return ApiResponse<OidcClientResponse>.Success(MapClient(c), "修改成功");
    }

    /// <summary>删除接入应用</summary>
    [HttpDelete("{id}")]
    public ApiResponse<object?> Delete(string id)
    {
        var client = _context.OidcClients.FirstOrDefault(c => c.Id == id);
        if (client == null)
            return ApiResponse<object?>.Fail("应用不存在", 404);
        _context.OidcClients.Remove(client);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }
}
