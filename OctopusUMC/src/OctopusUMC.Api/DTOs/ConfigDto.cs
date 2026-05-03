namespace OctopusUMC.Api.DTOs;

/// <summary>系统配置响应</summary>
public class ConfigResponse
{
    public long ConfigId { get; set; }
    public string ConfigName { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public bool ConfigType { get; set; }
    public DateTime CreateTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>创建配置请求</summary>
public class CreateConfigRequest
{
    public string ConfigName { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public bool ConfigType { get; set; }
    public string? Remark { get; set; }
}

/// <summary>修改配置请求</summary>
public class UpdateConfigRequest
{
    public long ConfigId { get; set; }
    public string ConfigName { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public bool ConfigType { get; set; }
    public string? Remark { get; set; }
}
