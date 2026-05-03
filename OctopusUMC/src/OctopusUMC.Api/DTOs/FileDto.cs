namespace OctopusUMC.Api.DTOs;

/// <summary>文件信息响应</summary>
public class OssFileResponse
{
    public long OssId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string FileSuffix { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Service { get; set; } = "local";
    public string CreateBy { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}
