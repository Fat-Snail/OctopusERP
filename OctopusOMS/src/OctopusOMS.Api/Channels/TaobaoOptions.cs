namespace OctopusOMS.Api.Channels;

/// <summary>
/// 淘宝开放平台（TOP）接入配置。AppKey / AppSecret / Session 任一缺失时适配器自动进入
/// 确定性仿真模式；三项齐全后无需改代码即可切换为真实 API 调用。
/// </summary>
public class TaobaoOptions
{
    /// <summary>开放平台应用 Key</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>开放平台应用 Secret（用于 MD5 签名）</summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>OAuth 授权后的 access_token（TOP 参数 session）</summary>
    public string Session { get; set; } = string.Empty;

    /// <summary>TOP 网关地址</summary>
    public string ApiUrl { get; set; } = "https://gw.api.taobao.com/router/rest";

    /// <summary>强制仿真（即使凭证齐全，例如演示环境）</summary>
    public bool Simulated { get; set; }

    /// <summary>taobao.trades.sold.get 每页条数（TOP 上限 40）</summary>
    public int PageSize { get; set; } = 40;

    /// <summary>单个时间窗口最多翻页数（防护，正常远小于此值）</summary>
    public int MaxPagesPerWindow { get; set; } = 25;

    /// <summary>限流重试等待毫秒数</summary>
    public int RetryDelayMs { get; set; } = 1000;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(AppKey) &&
        !string.IsNullOrWhiteSpace(AppSecret) &&
        !string.IsNullOrWhiteSpace(Session);
}
