namespace OctopusUMC.Api.Services;

public record WechatTokenResult(string OpenId, string AccessToken, string? UnionId);
public record WechatUserInfo(string OpenId, string NickName, string? HeadImgUrl, string? UnionId);

public interface IWechatOAuthService
{
    string BuildAuthUrl(string state);
    Task<WechatTokenResult?> ExchangeCodeAsync(string code);
    Task<WechatUserInfo?> GetUserInfoAsync(string accessToken, string openId);
}
