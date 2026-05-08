using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OctopusUMC.Api.Attributes;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;
using System.Text.Json;

namespace OctopusUMC.Api.Filters;

public class OperLogFilter(ApplicationDbContext db) : IActionFilter
{
    private DateTime _startTime;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        _startTime = DateTime.UtcNow;
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<LogAttribute>()
            .FirstOrDefault();
        if (attr == null) return;

        var http = context.HttpContext;
        var userName = http.User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var jsonResult = context.Result is ObjectResult objResult
            ? JsonSerializer.Serialize(objResult.Value)
            : string.Empty;

        db.OperLogs.Add(new OperLog
        {
            Title = attr.Title,
            OperName = userName,
            OperUrl = http.Request.Path,
            RequestMethod = http.Request.Method,
            OperParam = http.Request.QueryString.Value ?? string.Empty,
            JsonResult = jsonResult,
            Status = context.Exception == null ? 0 : 1,
            ErrorMsg = context.Exception?.Message,
            OperTime = _startTime,
            CostTime = (long)(DateTime.UtcNow - _startTime).TotalMilliseconds,
        });
        db.SaveChanges();
    }
}
