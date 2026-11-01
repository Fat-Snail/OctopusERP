using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OctopusUMC.Api.Middleware;
using System.Text.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// P8 — GlobalExceptionMiddleware 单元测试
/// 验证：
/// - 任何未捕获异常 → 返回 HTTP 500 + JSON { code:500, msg:"服务器内部错误", data:null }
/// - 不暴露异常类型 / 堆栈 / Inner exception
/// - 正常请求透传（无异常时不修改 status code）
/// </summary>
public class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_OnException_Returns500WithUnifiedFormat_AndHidesStack()
    {
        // Arrange — 模拟 next 抛出敏感错误
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("DB connection string secret=xyz failed"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert — 状态码、内容类型
        ctx.Response.StatusCode.Should().Be(500);
        ctx.Response.ContentType.Should().Be("application/json");

        // 读响应体
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();

        // 必须是统一 JSON 格式
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(500);
        doc.RootElement.GetProperty("msg").GetString().Should().Be("服务器内部错误");
        doc.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);

        // 不能暴露任何敏感信息
        body.Should().NotContain("InvalidOperationException");
        body.Should().NotContain("DB connection string");
        body.Should().NotContain("secret=xyz");
        body.Should().NotContain("StackTrace");
        body.Should().NotContain(" at ");  // 堆栈帧的典型标记
    }

    [Fact]
    public async Task InvokeAsync_OnSuccess_DoesNotModifyResponse()
    {
        // Arrange — next 正常完成（写入 200 + payload）
        var middleware = new GlobalExceptionMiddleware(
            async c =>
            {
                c.Response.StatusCode = 200;
                c.Response.ContentType = "application/json";
                await c.Response.WriteAsync("{\"code\":200,\"msg\":\"ok\",\"data\":1}");
            },
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(200);
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("\"code\":200");
        body.Should().NotContain("服务器内部错误");
    }

    [Fact]
    public async Task InvokeAsync_OnNullReferenceException_StillReturnsUnifiedFormat()
    {
        // 边界：常见 NRE 也应被吃掉，不抛栈到客户端
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new NullReferenceException("Object reference not set"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(500);
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().NotContain("NullReferenceException");
        body.Should().NotContain("Object reference not set");
    }
}
