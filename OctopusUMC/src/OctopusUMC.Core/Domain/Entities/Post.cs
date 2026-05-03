namespace OctopusUMC.Core.Domain.Entities;

public class Post
{
    public long PostId { get; set; }
    public string PostName { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
    public int PostSort { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }
}
