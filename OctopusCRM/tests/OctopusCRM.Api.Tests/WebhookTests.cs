using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OctopusCRM.Api.Persistence;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusCRM.Api.Tests;

public class WebhookTests : IClassFixture<CrmTestFactory>
{
    private readonly HttpClient _client;
    private readonly CrmTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private const string CrmSecret = "crm-shared-secret-dev";

    public WebhookTests(CrmTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static string ComputeHmac(string body, string secret = CrmSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();
    }

    private async Task<HttpResponseMessage> PostWithSignature(string path, object payload, string? secret = null)
    {
        var body = JsonSerializer.Serialize(payload);
        var sig = ComputeHmac(body, secret ?? CrmSecret);
        var req = new HttpRequestMessage(HttpMethod.Post, path)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.TryAddWithoutValidation("X-Crm-Signature", sig);
        return await _client.SendAsync(req);
    }

    private async Task<long> GetActiveContractIdAsync()
    {
        // Find or create a contract in active state using existing test infra
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var active = db.Contracts.FirstOrDefault(x => x.Status == "active");
        if (active != null) return active.ContractId;

        // Manually set a contract to active for testing
        var contract = db.Contracts.FirstOrDefault();
        if (contract == null) return 0;
        contract.Status = "active";
        contract.OaApprovalId = null;
        db.SaveChanges();
        return contract.ContractId;
    }

    [Fact]
    public async Task ShipConfirm_WithValidSignatureAndActiveContract_SetsStatusShipped()
    {
        var contractId = await GetActiveContractIdAsync();
        if (contractId == 0)
        {
            // No contracts in seed, test endpoint availability only
            var emptyResp = await PostWithSignature("/api/webhook/ship-confirm", new
            {
                CrmContractId = 99999L,
                ShippedAt = DateTime.UtcNow
            });
            emptyResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            return;
        }

        var resp = await PostWithSignature("/api/webhook/ship-confirm", new
        {
            CrmContractId = contractId,
            ShippedAt = DateTime.UtcNow
        });

        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var contract = db.Contracts.Find(contractId);
        contract!.Status.Should().Be("shipped");
        contract.ActualDeliveryDate.Should().NotBeNull();
    }

    [Fact]
    public async Task ShipConfirm_WithInvalidSignature_Returns401()
    {
        var resp = await PostWithSignature("/api/webhook/ship-confirm",
            new { CrmContractId = 1L, ShippedAt = DateTime.UtcNow },
            secret: "wrong-secret");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShipConfirm_WithoutSignatureHeader_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/webhook/ship-confirm", new
        {
            CrmContractId = 1L,
            ShippedAt = DateTime.UtcNow
        });
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShipConfirm_WithNonExistentContract_ReturnsOk()
    {
        var resp = await PostWithSignature("/api/webhook/ship-confirm", new
        {
            CrmContractId = 999999L,
            ShippedAt = DateTime.UtcNow
        });
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
