using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using OctopusMES.Api.Services.PlmSync;

namespace OctopusMES.Api.Tests;

public sealed class PlmManufacturingSyncTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;

    public PlmManufacturingSyncTests(MesTestFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-PLM-Secret", "mes-test-secret");
    }

    [Fact]
    public async Task ReleaseEvent_RejectsLegacyRequestWithoutTimestampAndPayloadHash()
    {
        var response = await _client.PostAsJsonAsync("/api/integration/plm/manufacturing-released", new
        {
            eventId = $"evt-{Guid.NewGuid():N}",
            eventType = "manufacturing.released",
            productId = 10,
            manufacturingVersionId = 20,
            releasedAt = DateTime.UtcNow,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReleaseEvent_RejectsStaleTimestamp()
    {
        var response = await _client.PostAsJsonAsync("/api/integration/plm/manufacturing-released", new
        {
            eventId = $"evt-{Guid.NewGuid():N}",
            eventType = "manufacturing.released",
            productId = 10,
            manufacturingVersionId = 20,
            releasedAt = DateTime.UtcNow.AddMinutes(-10),
            sentAt = DateTime.UtcNow.AddMinutes(-10),
            payloadHash = new string('a', 64),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReleaseEvent_RejectsRequestWithoutContentSignature()
    {
        var response = await _client.PostAsJsonAsync("/api/integration/plm/manufacturing-released", new
        {
            eventId = $"evt-{Guid.NewGuid():N}",
            eventType = "manufacturing.released",
            productId = 10,
            manufacturingVersionId = 20,
            releasedAt = DateTime.UtcNow,
            sentAt = DateTime.UtcNow,
            payloadHash = new string('a', 64),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void Authenticator_AcceptsValidSignatureAndRejectsInvalidHashFormat()
    {
        var now = DateTime.UtcNow;
        var evt = new PlmReleaseEvent("evt-1", "manufacturing_version_released", 2, 3, now, now, new string('a', 64));
        var canonical = PlmEventAuthenticator.CreateCanonical(evt);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("secret"), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        PlmEventAuthenticator.Authenticate(evt, "secret", "secret", signature, now).Success.Should().BeTrue();
        PlmEventAuthenticator.Authenticate(evt with { PayloadHash = "bad" }, "secret", "secret", signature, now)
            .ErrorCode.Should().Be("INVALID_PAYLOAD_HASH");
    }
}
