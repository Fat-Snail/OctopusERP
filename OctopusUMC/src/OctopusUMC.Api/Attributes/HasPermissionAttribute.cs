using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OctopusUMC.Api.DTOs;

namespace OctopusUMC.Api.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class HasPermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(
                new { code = 401, msg = "请先登录", data = (object?)null });
            return;
        }

        if (user.HasClaim("permission", "*:*:*")) return;

        if (!user.HasClaim("permission", permission))
        {
            context.Result = new OkObjectResult(
                ApiResponse<object?>.Fail("权限不足，无法执行此操作", 403));
        }
    }
}
