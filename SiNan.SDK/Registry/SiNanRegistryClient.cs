using System.Net;
using Microsoft.Extensions.Options;

namespace SiNan.SDK.Registry;

public sealed class SiNanRegistryClient : ISiNanRegistryClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<SiNanClientOptions> _options;

    public SiNanRegistryClient(IHttpClientFactory httpClientFactory, IOptions<SiNanClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<RegisterInstanceResponse> RegisterAsync(RegisterInstanceRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Post, "/api/v1/registry/register")
            {
                Content = HttpJson.CreateJsonContent(request)
            },
            options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }

        return (await HttpJson.ReadAsync<RegisterInstanceResponse>(response.Content, cancellationToken))!;
    }

    public async Task DeregisterAsync(DeregisterInstanceRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Post, "/api/v1/registry/deregister")
            {
                Content = HttpJson.CreateJsonContent(request)
            },
            options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }
    }

    public async Task<HeartbeatResponse> HeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Post, "/api/v1/registry/heartbeat")
            {
                Content = HttpJson.CreateJsonContent(request)
            },
            options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }

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

        var url = "/api/v1/registry/instances?" + string.Join("&", query);

        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () =>
            {
                var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(etag))
                {
                    retryRequest.Headers.TryAddWithoutValidation("If-None-Match", etag);
                }

                return retryRequest;
            },
            options,
            cancellationToken);
        var responseEtag = response.Headers.ETag?.Tag;

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new ApiResult<ServiceInstancesResponse>(response.StatusCode, null, responseEtag, true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }
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

        var url = "/api/v1/registry/subscribe?" + string.Join("&", query);

        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () =>
            {
                var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(etag))
                {
                    retryRequest.Headers.TryAddWithoutValidation("If-None-Match", etag);
                }

                return retryRequest;
            },
            options,
            cancellationToken);
        var responseEtag = response.Headers.ETag?.Tag;

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new ApiResult<ServiceInstancesResponse>(response.StatusCode, null, responseEtag, true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }
        var payload = await HttpJson.ReadAsync<ServiceInstancesResponse>(response.Content, cancellationToken);
        return new ApiResult<ServiceInstancesResponse>(response.StatusCode, payload, responseEtag, false);
    }
}
