using System.Diagnostics;
using System.Reflection;

namespace OctopusGateway.Api.Services;

/// <summary>单个下游的健康探测结果。</summary>
public record DownstreamHealth(
    string System,
    string Address,
    string Status,
    int? HttpStatus,
    long LatencyMs,
    string? Error);

/// <summary>全量健康聚合结果。overall = healthy / degraded / unhealthy。</summary>
public record HealthFullResult(
    string Overall,
    DateTimeOffset CheckedAt,
    int Total,
    int Healthy,
    int Unhealthy,
    IReadOnlyList<DownstreamHealth> Services);

/// <summary>
/// v1.5.5 可观测性：从网关视角聚合所有下游服务的健康状态。
/// 下游地址来自 ReverseProxy:Clusters，不额外维护清单——加一条路由即自动纳入探测。
/// 单个下游超时/异常只降级为 unhealthy，不影响其它探测，也不让端点本身失败。
/// </summary>
public class ObservabilityService(
    IConfiguration config,
    IHttpClientFactory httpClientFactory)
{
    private static readonly DateTimeOffset ProcessStartedAt = DateTimeOffset.UtcNow;

    public async Task<HealthFullResult> GetFullHealthAsync(CancellationToken ct)
    {
        var timeoutMs = config.GetValue("HealthCheck:TimeoutMs", 2000);
        var probePath = config["HealthCheck:ProbePath"] ?? "/health";

        var clusters = config.GetSection("ReverseProxy:Clusters").GetChildren().ToList();
        var client = httpClientFactory.CreateClient("gateway-health");

        // 并发探测，任一超时不影响其它；self 集群（指向网关自身）纳入，验证回环链路。
        var tasks = clusters.Select(c => ProbeAsync(c.Key, c["Destinations:d1:Address"], probePath, client, timeoutMs, ct));
        var results = (await Task.WhenAll(tasks)).ToList();

        var healthy = results.Count(r => r.Status == "healthy");
        var unhealthy = results.Count - healthy;
        var overall = unhealthy == 0 ? "healthy" : healthy == 0 ? "unhealthy" : "degraded";

        return new HealthFullResult(overall, DateTimeOffset.UtcNow, results.Count, healthy, unhealthy, results);
    }

    private async Task<DownstreamHealth> ProbeAsync(
        string system,
        string? address,
        string probePath,
        HttpClient client,
        int timeoutMs,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(address))
            return new DownstreamHealth(system, "", "unhealthy", null, 0, "no destination configured");

        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            var url = new Uri(new Uri(address), probePath.TrimStart('/')).ToString();
            using var response = await client.GetAsync(url, cts.Token);
            sw.Stop();
            return response.IsSuccessStatusCode
                ? new DownstreamHealth(system, address, "healthy", (int)response.StatusCode, sw.ElapsedMilliseconds, null)
                : new DownstreamHealth(system, address, "unhealthy", (int)response.StatusCode, sw.ElapsedMilliseconds,
                    $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new DownstreamHealth(system, address, "unhealthy", null, sw.ElapsedMilliseconds,
                $"timeout after {timeoutMs}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DownstreamHealth(system, address, "unhealthy", null, sw.ElapsedMilliseconds, ex.Message);
        }
    }

    /// <summary>
    /// 运行时诊断快照：只暴露非敏感的运行信息（版本/环境/运行时长/路由与集群拓扑），
    /// 不输出 JWT authority 之外的任何密钥或凭据。
    /// </summary>
    public object GetDiagnostics()
    {
        var clusters = config.GetSection("ReverseProxy:Clusters").GetChildren()
            .Select(c => new { System = c.Key, Address = c["Destinations:d1:Address"] })
            .ToList();
        var routes = config.GetSection("ReverseProxy:Routes").GetChildren()
            .Select(r => new { Route = r.Key, Match = r["Match:Path"], Cluster = r["ClusterId"] })
            .ToList();

        return new
        {
            service = "octopus-gateway",
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            environment = config["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            dotnet = Environment.Version.ToString(),
            processStartedAt = ProcessStartedAt,
            uptimeSeconds = (long)(DateTimeOffset.UtcNow - ProcessStartedAt).TotalSeconds,
            rateLimit = new
            {
                permitLimit = config.GetValue("RateLimit:PermitLimit", 120),
                windowSeconds = config.GetValue("RateLimit:WindowSeconds", 10),
            },
            routes,
            clusters,
        };
    }
}
