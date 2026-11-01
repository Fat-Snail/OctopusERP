using System.Net.Http.Json;

namespace OctopusMES.Api.Services.PlmSync;

public interface IPlmManufacturingClient
{
    Task<ManufacturingPayload> GetReleasedAsync(long productId, long manufacturingVersionId, CancellationToken ct);
}

public sealed class PlmManufacturingClient(IHttpClientFactory httpFactory, IConfiguration config) : IPlmManufacturingClient
{
    public async Task<ManufacturingPayload> GetReleasedAsync(long productId, long manufacturingVersionId, CancellationToken ct)
    {
        var client = httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var baseUrl = config["Plm:BaseUrl"] ?? "http://localhost:5003";
        var response = await client.GetFromJsonAsync<ApiWrapper<ManufacturingPayload>>(
            $"{baseUrl}/api/manufacturing/products/{productId}/versions/{manufacturingVersionId}", ct);
        if (response?.Code != 200 || response.Data is null)
            throw new PlmSyncException("PLM_PAYLOAD_UNAVAILABLE", response?.Msg ?? "PLM 已发布制造版本读取失败", true);
        return response.Data;
    }

    private sealed record ApiWrapper<T>(int Code, string? Msg, T? Data);
}
