using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SiNan.Server.Tests;

public sealed class RegistryEndpointsTests : IClassFixture<SiNanWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RegistryEndpointsTests(SiNanWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Heartbeat_Deregister_Works()
    {
        var register = new
        {
            Namespace = "default",
            Group = "DEFAULT_GROUP",
            ServiceName = "orders",
            Host = "127.0.0.1",
            Port = 8080,
            Weight = 100,
            TtlSeconds = 30,
            IsEphemeral = true,
            Metadata = new Dictionary<string, string> { ["zone"] = "a" }
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/registry/register", register);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(registerPayload.TryGetProperty("instanceId", out _));

        var heartbeat = new
        {
            Namespace = "default",
            Group = "DEFAULT_GROUP",
            ServiceName = "orders",
            Host = "127.0.0.1",
            Port = 8080
        };

        var heartbeatResponse = await _client.PostAsJsonAsync("/api/v1/registry/heartbeat", heartbeat);
        Assert.Equal(HttpStatusCode.OK, heartbeatResponse.StatusCode);

        var instancesResponse = await _client.GetAsync("/api/v1/registry/instances?namespace=default&group=DEFAULT_GROUP&serviceName=orders");
        Assert.Equal(HttpStatusCode.OK, instancesResponse.StatusCode);
        Assert.True(instancesResponse.Headers.ETag is not null);

        var etag = instancesResponse.Headers.ETag?.Tag ?? string.Empty;
        var cachedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/registry/instances?namespace=default&group=DEFAULT_GROUP&serviceName=orders");
        cachedRequest.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var cachedResponse = await _client.SendAsync(cachedRequest);
        Assert.Equal(HttpStatusCode.NotModified, cachedResponse.StatusCode);

        var deregister = new
        {
            Namespace = "default",
            Group = "DEFAULT_GROUP",
            ServiceName = "orders",
            Host = "127.0.0.1",
            Port = 8080
        };

        var deregisterResponse = await _client.PostAsJsonAsync("/api/v1/registry/deregister", deregister);
        Assert.Equal(HttpStatusCode.OK, deregisterResponse.StatusCode);

        var heartbeatMissing = await _client.PostAsJsonAsync("/api/v1/registry/heartbeat", heartbeat);
        Assert.Equal(HttpStatusCode.NotFound, heartbeatMissing.StatusCode);
    }

    [Fact]
    public async Task Register_Invalid_Request_Returns_BadRequest()
    {
        var register = new
        {
            Namespace = "",
            Group = "",
            ServiceName = "",
            Host = "",
            Port = 0,
            Weight = 0,
            TtlSeconds = 1,
            IsEphemeral = true,
            Metadata = new Dictionary<string, string>()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/registry/register", register);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
