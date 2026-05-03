namespace OctopusUMC.Api.DTOs;

/// <summary>OIDC 客户端响应</summary>
public class OidcClientResponse
{
    public string Id { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientType { get; set; } = "public";
    public List<string> RedirectUris { get; set; } = new();
    public List<string> PostLogoutRedirectUris { get; set; } = new();
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>创建 OIDC 客户端请求</summary>
public class CreateOidcClientRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientType { get; set; } = "public";
    public List<string> RedirectUris { get; set; } = new();
    public List<string> PostLogoutRedirectUris { get; set; } = new();
    public int Status { get; set; } = 1;
}

/// <summary>修改 OIDC 客户端请求</summary>
public class UpdateOidcClientRequest
{
    public string Id { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientType { get; set; } = "public";
    public List<string> RedirectUris { get; set; } = new();
    public List<string> PostLogoutRedirectUris { get; set; } = new();
    public int Status { get; set; } = 1;
}
