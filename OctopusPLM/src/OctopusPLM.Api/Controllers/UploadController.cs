using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace OctopusPLM.Api.Controllers;

[ApiController]
[Route("api/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private static readonly string[] AllowedMimes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private const long MaxSize = 10 * 1024 * 1024; // 10 MB

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return Ok(new { code = 400, msg = "请选择文件" });
        if (!AllowedMimes.Contains(file.ContentType.ToLowerInvariant()))
            return Ok(new { code = 400, msg = "仅支持 JPG / PNG / WebP / GIF 格式" });
        if (file.Length > MaxSize)
            return Ok(new { code = 400, msg = "文件大小不能超过 10 MB" });

        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{ext}";

        await using var stream = System.IO.File.Create(Path.Combine(uploadsDir, fileName));
        await file.CopyToAsync(stream);

        return Ok(new { code = 200, msg = "上传成功", data = new { url = $"/uploads/{fileName}" } });
    }
}
