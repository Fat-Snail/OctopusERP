using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace OctopusCommon.Security;

/// <summary>
/// 子系统之间内部接口的共享密钥解析。
///
/// 背景：UMC 与零售域之间有两个「内部」接口（建号、成员资料回推），
/// 用 <c>X-Internal-Secret</c> 头鉴权。早期实现是：
///
///     config["InternalApi:SharedSecret"] ?? "retail-internal-secret-dev"
///
/// 也就是**配置缺失时自动回退到一个公开在仓库里的开发密钥**。
/// 开发环境无所谓，但生产环境一旦漏配，等于把「可以直接建号」的接口
/// 用一个人人都知道的密码保护起来。
///
/// 这里的策略是 **fail-closed**：
/// - 显式配置了 → 用它
/// - 未配置且处于 Development / Test → 回退开发密钥（保证本地与测试可用）
/// - 其它环境（Staging / Production）未配置 → 返回 null，调用方必须**拒绝**请求
/// </summary>
public static class InternalApiSecret
{
    public const string ConfigKey = "InternalApi:SharedSecret";

    /// <summary>开发/测试环境的回退值，与 Aspire 编排里注入的一致</summary>
    public const string DevelopmentDefault = "retail-internal-secret-dev";

    /// <summary>
    /// 解析当前环境应使用的内部密钥。
    /// 返回 null 表示「未配置且不允许回退」，调用方必须拒绝该请求。
    /// </summary>
    public static string? Resolve(IConfiguration config, IHostEnvironment env)
    {
        var configured = config[ConfigKey];
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        return env.IsDevelopment() || env.IsEnvironment("Test")
            ? DevelopmentDefault
            : null;
    }

    /// <summary>恒定时间比较，避免通过响应耗时猜密钥</summary>
    public static bool Matches(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected)) return false;

        var a = System.Text.Encoding.UTF8.GetBytes(provided);
        var b = System.Text.Encoding.UTF8.GetBytes(expected);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }
}
