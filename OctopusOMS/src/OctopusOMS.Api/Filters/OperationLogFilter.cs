using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace OctopusOMS.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class OperationLogAttribute(string operationName) : Attribute
{
    public string OperationName { get; } = operationName;
}

public class OperationLogFilter(ILogger<OperationLogFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attr = context.ActionDescriptor.EndpointMetadata.OfType<OperationLogAttribute>().FirstOrDefault();
        if (attr is null) { await next(); return; }
        var sw = Stopwatch.StartNew();
        var executed = await next();
        sw.Stop();
        var userId = context.HttpContext.User.FindFirst("sub")?.Value ?? "anonymous";
        logger.LogInformation("[OPERATION] {Op} user={User} method={Method} path={Path} status={Status} elapsed={Ms}ms",
            attr.OperationName, userId, context.HttpContext.Request.Method,
            context.HttpContext.Request.Path, context.HttpContext.Response.StatusCode, sw.ElapsedMilliseconds);
        if (executed.Exception != null) logger.LogError(executed.Exception, "[OPERATION-EXCEPTION] {Op}", attr.OperationName);
    }
}
