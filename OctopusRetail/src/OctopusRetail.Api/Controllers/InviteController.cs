using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Dtos;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;

namespace OctopusRetail.Api.Controllers;

/// <summary>
/// 店铺邀请接口（design §5）。
///
/// 两类端点：
///   租户内（需合法 X-Tenant-Id）：创建 / 列表 / 撤销 —— 且必须 owner|manager
///   跨租户（[AllowCrossTenant]）：预览 / 接受 —— 被邀请人此时还不是成员
///
/// 注意：Keycloak Admin API 凭据绝不下发到前端或租户。建号与加组由本服务代理
/// （M3 的 GroupSyncReconciler 负责），租户只拿得到邀请链接。
/// </summary>
[ApiController]
[Authorize]
[Route("api/retail/invites")]
public class InviteController(
    InviteService invites,
    RetailDbContext db,
    ITenantContext tenant,
    CurrentUser currentUser,
    IConfiguration config,
    TimeProvider clock,
    TenantAuthorization auth) : ControllerBase
{
    private string PublicBaseUrl =>
        config["Retail:PublicBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5186";

    /// <summary>创建邀请（需店长/店主）</summary>
    [HttpPost]
    public async Task<ApiResponse<CreateInviteResponse>> Create(
        [FromBody] CreateInviteRequest req, CancellationToken ct)
    {
        var outcome = await invites.CreateAsync(currentUser.Sub, new CreateInviteInput(
            req.Phone, req.InviteCode, req.RoleInTenant, req.MaxUses, TimeSpan.FromHours(req.ValidHours)), ct);

        if (!outcome.Success || outcome.Invite is null || outcome.RawToken is null)
            return ApiResponse<CreateInviteResponse>.Fail(outcome.Error ?? "创建邀请失败", 403);

        var invite = outcome.Invite;
        return ApiResponse<CreateInviteResponse>.Success(new CreateInviteResponse
        {
            InviteId = invite.InviteId,
            TenantId = invite.TenantId,
            RoleInTenant = invite.RoleInTenant,
            MaxUses = invite.MaxUses,
            ExpiresAt = invite.ExpiresAt,
            Token = outcome.RawToken,
            InviteUrl = $"{PublicBaseUrl}/invite/accept?token={Uri.EscapeDataString(outcome.RawToken)}",
            ShortCode = outcome.ShortCode ?? string.Empty,
            ShortUrl = outcome.ShortCode is null
                ? string.Empty
                : $"{PublicBaseUrl}/i/{outcome.ShortCode}",
        }, "邀请已创建");
    }

    /// <summary>本租户邀请列表</summary>
    [HttpGet]
    public async Task<ApiResponse<object>> List(CancellationToken ct)
    {
        var items = (await invites.ListAsync(ct)).Select(i => new InviteResponse
        {
            InviteId = i.InviteId,
            TenantId = i.TenantId,
            Phone = i.Phone,
            InviteCode = i.InviteCode,
            RoleInTenant = i.RoleInTenant,
            MaxUses = i.MaxUses,
            UsedCount = i.UsedCount,
            State = i.State,
            ExpiresAt = i.ExpiresAt,
            CreatedAt = i.CreatedAt,
            AcceptedBySub = i.AcceptedBySub,
        }).ToList();

        return ApiResponse<object>.Success(new { tenantId = tenant.TenantId, items, total = items.Count });
    }

    /// <summary>撤销邀请（需店长/店主）</summary>
    [HttpPost("{inviteId:long}/revoke")]
    public async Task<ApiResponse<object?>> Revoke(long inviteId, CancellationToken ct)
    {
        var ok = await invites.RevokeAsync(inviteId, currentUser.Sub, ct);
        return ok
            ? ApiResponse<object?>.Success(null, "邀请已撤销")
            : ApiResponse<object?>.Fail("撤销失败：邀请不存在、已终结或无权操作", 403);
    }

    /// <summary>
    /// 预览邀请（接受前展示将加入的门店）。
    /// 匿名可访问：被邀请人此时通常还没有账号，需要先看到「加入哪家店」再决定注册还是登录。
    /// 保密性由邀请令牌本身承担（仅暴露门店名与角色，不泄露成员信息）。
    /// </summary>
    [HttpGet("preview")]
    [AllowAnonymous]
    [AllowCrossTenant]
    public async Task<ApiResponse<InvitePreviewResponse>> Preview(
        [FromQuery] string? token, [FromQuery] string? code, CancellationToken ct)
    {
        var credential = string.IsNullOrWhiteSpace(code) ? token : code;
        if (string.IsNullOrWhiteSpace(credential))
            return ApiResponse<InvitePreviewResponse>.Fail("缺少邀请令牌或短码", 400);

        var invite = await invites.FindByCredentialAsync(credential, ct);
        if (invite is null)
            return ApiResponse<InvitePreviewResponse>.Fail("邀请不存在或已失效", 404);

        var target = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == invite.TenantId, ct);

        var usable = invite.State == InviteState.Pending
                     && invite.UsedCount < invite.MaxUses
                     && invite.ExpiresAt > clock.GetUtcNow().UtcDateTime;

        // 匿名端点，但若请求带了有效 token，就能判定"你是不是已经在这个组织里"
        string? myRole = null;
        if (!string.IsNullOrEmpty(currentUser.Sub))
            myRole = await auth.GetRoleAsync(currentUser.Sub, invite.TenantId, ct);

        return ApiResponse<InvitePreviewResponse>.Success(new InvitePreviewResponse
        {
            TenantId = invite.TenantId,
            TenantName = target?.TenantName ?? invite.TenantId,
            TenantType = target?.TenantType ?? "store",
            RoleInTenant = invite.RoleInTenant,
            ExpiresAt = invite.ExpiresAt,
            Usable = usable,
            Reason = usable ? null : "邀请已被使用或已过期",
            AlreadyMember = myRole is not null,
            MyRole = myRole,
        });
    }

    /// <summary>接受邀请</summary>
    [HttpPost("accept")]
    [AllowCrossTenant]
    public async Task<ApiResponse<object>> Accept([FromBody] AcceptInviteRequest req, CancellationToken ct)
    {
        var outcome = await invites.AcceptAsync(req.Code ?? req.Token, currentUser.Sub, ct);
        if (!outcome.Success)
            return ApiResponse<object>.Fail(outcome.Error ?? "接受邀请失败", 400);

        return ApiResponse<object>.Success(new
        {
            tenantId = outcome.TenantId,
            roleInTenant = outcome.RoleInTenant,
            alreadyMember = outcome.AlreadyMember,
        }, outcome.AlreadyMember ? "你已是该门店成员" : "已加入门店");
    }

    /// <summary>
    /// 受控自助建号（无账号的被邀请人）。
    ///
    /// 匿名端点 —— 授权凭据是邀请令牌本身（经短信/企微送出）。
    /// 由本服务用 retail-invite-bot 服务账号建号，因此不需要打开 realm 的
    /// registrationAllowed（那等于对外开放注册）。
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [AllowCrossTenant]
    public async Task<ApiResponse<object>> Register(
        [FromBody] RegisterInviteRequest req, CancellationToken ct)
    {
        var outcome = await invites.RegisterAsync(
            req.Code ?? req.Token, req.UserName, req.DisplayName, req.Phone, req.Password, req.Email, ct);

        if (!outcome.Success)
            return ApiResponse<object>.Fail(outcome.Error ?? "注册失败", 400);

        return ApiResponse<object>.Success(new
        {
            userName = outcome.UserName,
            tenantId = outcome.TenantId,
        }, "账号已创建，请使用新账号登录");
    }
}
