namespace OctopusUMC.Api.DTOs;

/// <summary>字典类型响应</summary>
public class DictTypeResponse
{
    public long DictId { get; set; }
    public string DictName { get; set; } = string.Empty;
    public string DictType { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>字典数据响应</summary>
public class DictDataResponse
{
    public long DictCode { get; set; }
    public string DictType { get; set; } = string.Empty;
    public string DictLabel { get; set; } = string.Empty;
    public string DictValue { get; set; } = string.Empty;
    public int DictSort { get; set; }
    public int Status { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreateTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>创建字典类型请求</summary>
public class CreateDictTypeRequest
{
    public string DictName { get; set; } = string.Empty;
    public string DictType { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>修改字典类型请求</summary>
public class UpdateDictTypeRequest
{
    public long DictId { get; set; }
    public string DictName { get; set; } = string.Empty;
    public string DictType { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>创建字典数据请求</summary>
public class CreateDictDataRequest
{
    public string DictType { get; set; } = string.Empty;
    public string DictLabel { get; set; } = string.Empty;
    public string DictValue { get; set; } = string.Empty;
    public int DictSort { get; set; }
    public int Status { get; set; } = 1;
    public bool IsDefault { get; set; }
    public string? Remark { get; set; }
}

/// <summary>修改字典数据请求</summary>
public class UpdateDictDataRequest
{
    public long DictCode { get; set; }
    public string DictType { get; set; } = string.Empty;
    public string DictLabel { get; set; } = string.Empty;
    public string DictValue { get; set; } = string.Empty;
    public int DictSort { get; set; }
    public int Status { get; set; } = 1;
    public bool IsDefault { get; set; }
    public string? Remark { get; set; }
}
