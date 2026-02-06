using System.Net;

namespace SiNan.SDK.Registry;

public sealed class SiNanRegistryClient : ISiNanRegistryClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SiNanRegistryClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<RegisterInstanceResponse> RegisterAsync(RegisterInstanceRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await HttpJson.PostAsync(client, "/api/v1/registry/register", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await HttpJson.ReadAsync<RegisterInstanceResponse>(response.Content, cancellationToken))!;
    }

    public async Task DeregisterAsync(DeregisterInstanceRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await HttpJson.PostAsync(client, "/api/v1/registry/deregister", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<HeartbeatResponse> HeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await HttpJson.PostAsync(client, "/api/v1/registry/heartbeat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await HttpJson.ReadAsync<HeartbeatResponse>(response.Content, cancellationToken))!;
    }

    public async Task<ApiResult<ServiceInstancesResponse>> GetInstancesAsync(string @namespace, string group, string serviceName, bool healthyOnly = true, string? etag = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"namespace={Uri.EscapeDataString(@namespace)}",
            $"group={Uri.EscapeDataString(group)}",
            $"serviceName={Uri.EscapeDataString(serviceName)}",
            $"healthyOnly={healthyOnly.ToString().ToLowerInvariant()}"
        };

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/registry/instances?" + string.Join("&", query));
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await client.SendAsync(request, cancellationToken);
        var responseEtag = response.Headers.ETag?.Tag;

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new ApiResult<ServiceInstancesResponse>(response.StatusCode, null, responseEtag, true);
        }

        response.EnsureSuccessStatusCode();
        var payload = await HttpJson.ReadAsync<ServiceInstancesResponse>(response.Content, cancellationToken);
        return new ApiResult<ServiceInstancesResponse>(response.StatusCode, payload, responseEtag, false);
    }

    public async Task<ApiResult<ServiceInstancesResponse>> SubscribeAsync(string @namespace, string group, string serviceName, bool healthyOnly = true, int? timeoutMs = null, string? etag = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"namespace={Uri.EscapeDataString(@namespace)}",
            $"group={Uri.EscapeDataString(group)}",
            $"serviceName={Uri.EscapeDataString(serviceName)}",
            $"healthyOnly={healthyOnly.ToString().ToLowerInvariant()}"
        };

        if (timeoutMs.HasValue)
        {
            query.Add($"timeoutMs={timeoutMs.Value}");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/registry/subscribe?" + string.Join("&", query));
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await client.SendAsync(request, cancellationToken);
        var responseEtag = response.Headers.ETag?.Tag;

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new ApiResult<ServiceInstancesResponse>(response.StatusCode, null, responseEtag, true);
        }

        response.EnsureSuccessStatusCode();
        var payload = await HttpJson.ReadAsync<ServiceInstancesResponse>(response.Content, cancellationToken);
        return new ApiResult<ServiceInstancesResponse>(response.StatusCode, payload, responseEtag, false);
    }
}
