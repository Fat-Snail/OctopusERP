namespace OctopusUMC.Core.Domain.Entities;

public class OidcClient
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientType { get; set; } = "public";
    public List<string> RedirectUris { get; set; } = new();
    public List<string> PostLogoutRedirectUris { get; set; } = new();
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
