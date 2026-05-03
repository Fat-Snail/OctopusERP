namespace OctopusUMC.Api.DTOs;

/// <summary>职位信息响应</summary>
public class PostResponse
{
    public long PostId { get; set; }
    public string PostName { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
    public int PostSort { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>创建职位请求</summary>
public class CreatePostRequest
{
    public string PostName { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
    public int PostSort { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>修改职位请求</summary>
public class UpdatePostRequest
{
    public long PostId { get; set; }
    public string PostName { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
    public int PostSort { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}
