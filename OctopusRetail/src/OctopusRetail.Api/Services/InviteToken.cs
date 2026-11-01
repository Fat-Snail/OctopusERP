using System.Security.Cryptography;
using System.Text;

namespace OctopusRetail.Api.Services;

/// <summary>
/// 邀请令牌的生成与哈希。
///
/// 安全约定：明文 token 只在创建时返回一次（放进邀请链接发给被邀请人），
/// 数据库只保存 SHA-256 哈希。这样即使库被读走，也无法反推出可用的邀请链接。
/// </summary>
public static class InviteToken
{
    /// <summary>32 字节密码学随机数 → Base64Url（约 43 字符）</summary>
    public static string Generate() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// 短码字符表：去掉易混淆的 0/O、1/l/I、5/S，共 57 个字符。
    /// 8 位 ≈ 55^8 ≈ 8.4e13 种组合（约 46 bit）；配合有效期与使用次数上限，
    /// 足以作为邀请凭据，且可口头/短信传达不出错。
    /// </summary>
    public const string ShortCodeAlphabet =
        "2346789ABCDEFGHJKLMNPQRTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public const int ShortCodeLength = 8;

    /// <summary>生成短码，形如 B1V2d9xK</summary>
    public static string GenerateShortCode()
    {
        var chars = new char[ShortCodeLength];
        for (var i = 0; i < ShortCodeLength; i++)
            chars[i] = ShortCodeAlphabet[RandomNumberGenerator.GetInt32(ShortCodeAlphabet.Length)];
        return new string(chars);
    }

    /// <summary>SHA-256 十六进制小写</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
