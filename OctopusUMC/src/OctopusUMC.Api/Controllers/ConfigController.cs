using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>系统配置管理接口</summary>
[ApiController]
[Route("api/system/config")]
public class ConfigController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private const string CacheKeyPrefix = "sys_config:";
    public ConfigController(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private ConfigResponse MapConfig(SysConfig c) => new()
    {
        ConfigId = c.ConfigId,
        ConfigName = c.ConfigName,
        ConfigKey = c.ConfigKey,
        ConfigValue = c.ConfigValue,
        ConfigType = c.ConfigType,
        CreateTime = c.CreateTime,
        Remark = c.Remark,
    };

    /// <summary>分页查询配置列表</summary>
    [HttpGet("list")]
    public ApiResponse<PagedResult<ConfigResponse>> GetList(
        [FromQuery] string? configName,
        [FromQuery] string? configKey,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.Configs.AsQueryable();
        if (!string.IsNullOrEmpty(configName))
            query = query.Where(c => c.ConfigName.Contains(configName));
        if (!string.IsNullOrEmpty(configKey))
            query = query.Where(c => c.ConfigKey.Contains(configKey));

        var total = query.Count();
        var rows = query.Skip((pageNum - 1) * pageSize).Take(pageSize).ToList().Select(MapConfig).ToList();
        return ApiResponse<PagedResult<ConfigResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>根据配置ID获取详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<ConfigResponse> GetById(long id)
    {
        var c = _context.Configs.FirstOrDefault(c => c.ConfigId == id);
        if (c == null) return ApiResponse<ConfigResponse>.Fail("配置不存在", 404);
        return ApiResponse<ConfigResponse>.Success(MapConfig(c));
    }

    /// <summary>根据配置Key获取配置值（结果缓存10分钟）</summary>
    [HttpGet("key/{configKey}")]
    public ApiResponse<string> GetByKey(string configKey)
    {
        if (_cache.TryGetValue(CacheKeyPrefix + configKey, out string? cached))
            return ApiResponse<string>.Success(cached!);

        var c = _context.Configs.FirstOrDefault(c => c.ConfigKey == configKey);
        if (c == null) return ApiResponse<string>.Fail("配置不存在", 404);

        _cache.Set(CacheKeyPrefix + configKey, c.ConfigValue, TimeSpan.FromMinutes(10));
        return ApiResponse<string>.Success(c.ConfigValue);
    }

    /// <summary>刷新配置缓存</summary>
    [HttpPut("refreshCache")]
    public ApiResponse<object?> RefreshCache()
    {
        var allKeys = _context.Configs.Select(c => c.ConfigKey).ToList();
        foreach (var key in allKeys)
            _cache.Remove(CacheKeyPrefix + key);
        return ApiResponse<object?>.Success(null, "缓存已刷新");
    }

    /// <summary>新增系统配置</summary>
    [HttpPost]
    public ApiResponse<ConfigResponse> Create([FromBody] CreateConfigRequest req)
    {
        if (_context.Configs.Any(c => c.ConfigKey == req.ConfigKey))
            return ApiResponse<ConfigResponse>.Fail("配置键已存在");

        var config = new SysConfig
        {
            ConfigName = req.ConfigName,
            ConfigKey = req.ConfigKey,
            ConfigValue = req.ConfigValue,
            ConfigType = req.ConfigType,
            Remark = req.Remark,
            CreateTime = DateTime.UtcNow,
        };
        _context.Configs.Add(config);
        _context.SaveChanges();
        return ApiResponse<ConfigResponse>.Success(MapConfig(config), "新增成功");
    }

    /// <summary>修改系统配置</summary>
    [HttpPut]
    public ApiResponse<ConfigResponse> Update([FromBody] UpdateConfigRequest req)
    {
        var c = _context.Configs.FirstOrDefault(c => c.ConfigId == req.ConfigId);
        if (c == null) return ApiResponse<ConfigResponse>.Fail("配置不存在", 404);
        _cache.Remove(CacheKeyPrefix + c.ConfigKey);
        c.ConfigName = req.ConfigName;
        c.ConfigKey = req.ConfigKey;
        c.ConfigValue = req.ConfigValue;
        c.ConfigType = req.ConfigType;
        c.Remark = req.Remark;
        _context.SaveChanges();
        return ApiResponse<ConfigResponse>.Success(MapConfig(c), "修改成功");
    }

    /// <summary>批量删除配置（逗号分隔ID）</summary>
    [HttpDelete("{ids}")]
    public ApiResponse<object?> Delete(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.Configs.Where(c => idList.Contains(c.ConfigId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("配置不存在", 404);
        foreach (var item in items) _cache.Remove(CacheKeyPrefix + item.ConfigKey);
        _context.Configs.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }
}
