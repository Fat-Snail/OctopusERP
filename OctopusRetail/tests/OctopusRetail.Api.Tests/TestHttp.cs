using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace OctopusRetail.Api.Tests;

/// <summary>记录请求 + 按路径返回预设响应的 HTTP 处理器（M3 建号流程测试用）</summary>
public sealed class RecordingHttpHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> Bodies { get; } = new();
    public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        Requests.Add(req);
        Bodies.Add(req.Content is null ? "" : await req.Content.ReadAsStringAsync(ct));

        if (req.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token"))
            return Json(new { access_token = "fake", expires_in = 300 });

        return Responder?.Invoke(req) ?? new HttpResponseMessage(HttpStatusCode.OK);
    }

    public static HttpResponseMessage Json(object o, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = JsonContent.Create(o) };

    public static HttpResponseMessage Raw(string s, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(s, Encoding.UTF8, "application/json") };

    public int Count(HttpMethod m, Func<string, bool> match) =>
        Requests.Count(r => r.Method == m && match(r.RequestUri!.AbsolutePath));

    public bool Has(HttpMethod m, Func<string, bool> match) => Count(m, match) > 0;

    public string BodyOf(HttpMethod m, Func<string, bool> match)
    {
        var i = Requests.FindIndex(r => r.Method == m && match(r.RequestUri!.AbsolutePath));
        return i < 0 ? "" : Bodies[i];
    }

    /// <summary>重置 KeycloakAdminClient 的静态 token 缓存</summary>
    public static void ResetKeycloakTokenCache()
    {
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var t = typeof(OctopusRetail.Api.Services.KeycloakAdminClient);
        t.GetField("_cachedToken", flags)?.SetValue(null, null);
        t.GetField("_cachedTokenExpiry", flags)?.SetValue(null, DateTime.MinValue);
    }
}

public sealed class RecordingHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler) { Timeout = TimeSpan.FromSeconds(5) };
}
