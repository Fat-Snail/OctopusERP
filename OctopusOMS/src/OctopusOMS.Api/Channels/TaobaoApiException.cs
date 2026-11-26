namespace OctopusOMS.Api.Channels;

/// <summary>淘宝开放平台返回的 error_response（保留平台错误码便于排障）</summary>
public class TaobaoApiException(string message, string? code = null, string? subCode = null) : Exception(message)
{
    public string? Code { get; } = code;
    public string? SubCode { get; } = subCode;
}
