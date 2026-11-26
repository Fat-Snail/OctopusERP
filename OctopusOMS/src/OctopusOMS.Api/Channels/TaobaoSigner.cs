using System.Security.Cryptography;
using System.Text;

namespace OctopusOMS.Api.Channels;

/// <summary>
/// 淘宝开放平台 TOP 签名：所有非空业务/系统参数（排除 sign）按 key 字典序拼接 key+value，
/// 前后拼接 AppSecret 后取 MD5 并转大写。
/// </summary>
public static class TaobaoSigner
{
    public static string Sign(IReadOnlyDictionary<string, string> parameters, string secret)
    {
        var concat = string.Concat(parameters
            .Where(p => !string.Equals(p.Key, "sign", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => p.Key + p.Value));

        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(secret + concat + secret)));
    }
}
