using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;
using System.Security.Claims;

namespace OctopusMES.Api.Filters;

/// <summary>标记需要记录操作日志的动作。</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OperationLogAttribute(string operationName) : Attribute
{
    public string OperationName { get; } = operationName;
}

/// <summary>全局 Action Filter：对标注了 [OperationLog] 的接口写操作日志。</summary>
public class OperationLogFilter(ILogger<OperationLogFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<OperationLogAttribute>()
            .FirstOrDefault();

        if (attr is null) { await next(); return; }

        var sw = Stopwatch.StartNew();
        var userId = context.HttpContext.User.FindFirstValue("sub") ?? "anonymous";
        var method = context.HttpContext.Request.Method;
        var path = context.HttpContext.Request.Path;

        var result = await next();
        sw.Stop();

        var statusCode = context.HttpContext.Response.StatusCode;
        if (result.Exception is null)
        {
            logger.LogInformation(
                "[OperLog] {Op} user={User} {Method} {Path} → {Status} {Ms}ms",
                attr.OperationName, userId, method, path, statusCode, sw.ElapsedMilliseconds);
        }
        else
        {
            logger.LogWarning(
                "[OperLog] {Op} user={User} {Method} {Path} → EXCEPTION {Ms}ms: {Msg}",
                attr.OperationName, userId, method, path, sw.ElapsedMilliseconds, result.Exception.Message);
        }
    }
}
