namespace OctopusRetail.Api.Dtos;

/// <summary>自助创建租户（门店）请求</summary>
public class CreateTenantRequest
{
    /// <summary>租户编码，字母/数字/下划线/连字符，2-32 位；用于派生 group 名 t_{code}</summary>
    public string TenantCode { get; set; } = string.Empty;

    /// <summary>门店展示名</summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>store | chain | franchise，默认 store</summary>
    public string TenantType { get; set; } = "store";

    /// <summary>上级租户（加盟店挂在连锁主体下），可空</summary>
    public string? ParentTenantId { get; set; }
}

/// <summary>自助建店响应</summary>
public class CreateTenantResponse
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    /// <summary>owner 是否已成功加入 Keycloak 组（false 时由对账器重试）</summary>
    public bool GroupSynced { get; set; }

    /// <summary>
    /// 重要提示：当前 access token 里还没有新租户的 groups claim。
    /// 客户端需要重新获取 token（刷新/重新登录）才能切换过去。
    /// </summary>
    public string NextStep { get; set; } = string.Empty;
}
