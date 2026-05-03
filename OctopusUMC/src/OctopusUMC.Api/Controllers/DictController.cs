using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>字典管理接口</summary>
[ApiController]
[Route("api/system/dict")]
public class DictController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public DictController(ApplicationDbContext context) => _context = context;

    private DictTypeResponse MapType(DictType t) => new()
    {
        DictId = t.DictId,
        DictName = t.DictName,
        DictType = t.DictTypeCode,
        Status = t.Status,
        CreateTime = t.CreateTime,
        Remark = t.Remark,
    };

    private DictDataResponse MapData(DictData d) => new()
    {
        DictCode = d.DictCode,
        DictType = d.DictType,
        DictLabel = d.DictLabel,
        DictValue = d.DictValue,
        DictSort = d.DictSort,
        Status = d.Status,
        IsDefault = d.IsDefault,
        CreateTime = d.CreateTime,
        Remark = d.Remark,
    };

    // ── 字典类型 ────────────────────────────────────────

    /// <summary>分页查询字典类型列表</summary>
    [HttpGet("type/list")]
    public ApiResponse<PagedResult<DictTypeResponse>> GetTypeList(
        [FromQuery] string? dictName,
        [FromQuery] string? dictType,
        [FromQuery] int? status,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.DictTypes.AsQueryable();
        if (!string.IsNullOrEmpty(dictName))
            query = query.Where(t => t.DictName.Contains(dictName));
        if (!string.IsNullOrEmpty(dictType))
            query = query.Where(t => t.DictTypeCode.Contains(dictType));
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var total = query.Count();
        var rows = query.Skip((pageNum - 1) * pageSize).Take(pageSize).ToList().Select(MapType).ToList();
        return ApiResponse<PagedResult<DictTypeResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>根据字典类型ID获取详情</summary>
    [HttpGet("type/{id:long}")]
    public ApiResponse<DictTypeResponse> GetTypeById(long id)
    {
        var t = _context.DictTypes.FirstOrDefault(t => t.DictId == id);
        if (t == null) return ApiResponse<DictTypeResponse>.Fail("字典类型不存在", 404);
        return ApiResponse<DictTypeResponse>.Success(MapType(t));
    }

    /// <summary>新增字典类型</summary>
    [HttpPost("type")]
    public ApiResponse<DictTypeResponse> CreateType([FromBody] CreateDictTypeRequest req)
    {
        if (_context.DictTypes.Any(t => t.DictTypeCode == req.DictType))
            return ApiResponse<DictTypeResponse>.Fail("字典类型已存在");

        var dt = new DictType
        {
            DictName = req.DictName,
            DictTypeCode = req.DictType,
            Status = req.Status,
            Remark = req.Remark,
            CreateTime = DateTime.UtcNow,
        };
        _context.DictTypes.Add(dt);
        _context.SaveChanges();
        return ApiResponse<DictTypeResponse>.Success(MapType(dt), "新增成功");
    }

    /// <summary>修改字典类型</summary>
    [HttpPut("type")]
    public ApiResponse<DictTypeResponse> UpdateType([FromBody] UpdateDictTypeRequest req)
    {
        var t = _context.DictTypes.FirstOrDefault(t => t.DictId == req.DictId);
        if (t == null) return ApiResponse<DictTypeResponse>.Fail("字典类型不存在", 404);
        t.DictName = req.DictName;
        t.DictTypeCode = req.DictType;
        t.Status = req.Status;
        t.Remark = req.Remark;
        _context.SaveChanges();
        return ApiResponse<DictTypeResponse>.Success(MapType(t), "修改成功");
    }

    /// <summary>批量删除字典类型（逗号分隔ID）</summary>
    [HttpDelete("type/{ids}")]
    public ApiResponse<object?> DeleteType(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.DictTypes.Where(t => idList.Contains(t.DictId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("字典类型不存在", 404);
        _context.DictTypes.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }

    // ── 字典数据 ────────────────────────────────────────

    /// <summary>根据字典类型编码获取字典数据列表</summary>
    [HttpGet("data/type/{dictType}")]
    public ApiResponse<List<DictDataResponse>> GetDataByType(string dictType)
    {
        var list = _context.DictDatas
            .Where(d => d.DictType == dictType)
            .OrderBy(d => d.DictSort)
            .ToList()
            .Select(MapData).ToList();
        return ApiResponse<List<DictDataResponse>>.Success(list);
    }

    /// <summary>分页查询字典数据列表（支持筛选）</summary>
    [HttpGet("data/list")]
    public ApiResponse<PagedResult<DictDataResponse>> GetDataList(
        [FromQuery] string? dictType,
        [FromQuery] string? dictLabel,
        [FromQuery] int? status,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.DictDatas.AsQueryable();
        if (!string.IsNullOrEmpty(dictType))
            query = query.Where(d => d.DictType == dictType);
        if (!string.IsNullOrEmpty(dictLabel))
            query = query.Where(d => d.DictLabel.Contains(dictLabel));
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        var total = query.Count();
        var rows = query.OrderBy(d => d.DictSort)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .ToList()
            .Select(MapData).ToList();
        return ApiResponse<PagedResult<DictDataResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>新增字典数据</summary>
    [HttpPost("data")]
    public ApiResponse<DictDataResponse> CreateData([FromBody] CreateDictDataRequest req)
    {
        var data = new DictData
        {
            DictType = req.DictType,
            DictLabel = req.DictLabel,
            DictValue = req.DictValue,
            DictSort = req.DictSort,
            Status = req.Status,
            IsDefault = req.IsDefault,
            Remark = req.Remark,
            CreateTime = DateTime.UtcNow,
        };
        _context.DictDatas.Add(data);
        _context.SaveChanges();
        return ApiResponse<DictDataResponse>.Success(MapData(data), "新增成功");
    }

    /// <summary>修改字典数据</summary>
    [HttpPut("data")]
    public ApiResponse<DictDataResponse> UpdateData([FromBody] UpdateDictDataRequest req)
    {
        var d = _context.DictDatas.FirstOrDefault(d => d.DictCode == req.DictCode);
        if (d == null) return ApiResponse<DictDataResponse>.Fail("字典数据不存在", 404);
        d.DictType = req.DictType;
        d.DictLabel = req.DictLabel;
        d.DictValue = req.DictValue;
        d.DictSort = req.DictSort;
        d.Status = req.Status;
        d.IsDefault = req.IsDefault;
        d.Remark = req.Remark;
        _context.SaveChanges();
        return ApiResponse<DictDataResponse>.Success(MapData(d), "修改成功");
    }

    /// <summary>批量删除字典数据（逗号分隔ID）</summary>
    [HttpDelete("data/{ids}")]
    public ApiResponse<object?> DeleteData(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.DictDatas.Where(d => idList.Contains(d.DictCode)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("字典数据不存在", 404);
        _context.DictDatas.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }
}
