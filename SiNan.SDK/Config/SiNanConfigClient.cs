using System.Net;
using Microsoft.Extensions.Options;

namespace SiNan.SDK.Config;

public sealed class SiNanConfigClient : ISiNanConfigClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<SiNanClientOptions> _options;

    public SiNanConfigClient(IHttpClientFactory httpClientFactory, IOptions<SiNanClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<ConfigItemResponse> CreateAsync(ConfigUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Post, "/api/v1/configs")
            {
                Content = HttpJson.CreateJsonContent(request)
            },
            options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }

        return (await HttpJson.ReadAsync<ConfigItemResponse>(response.Content, cancellationToken))!;
    }

    public async Task<ConfigItemResponse> UpdateAsync(ConfigUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Put, "/api/v1/configs")
            {
                Content = HttpJson.CreateJsonContent(request)
            },
            options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }

        return (await HttpJson.ReadAsync<ConfigItemResponse>(response.Content, cancellationToken))!;
    }

    public async Task<ConfigItemResponse> GetAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/configs?namespace={Uri.EscapeDataString(@namespace)}&group={Uri.EscapeDataString(group)}&key={Uri.EscapeDataString(key)}";
        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Get, url),
            options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }

        return (await HttpJson.ReadAsync<ConfigItemResponse>(response.Content, cancellationToken))!;
    }

    public async Task DeleteAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/configs?namespace={Uri.EscapeDataString(@namespace)}&group={Uri.EscapeDataString(group)}&key={Uri.EscapeDataString(key)}";
        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Delete, url),
            options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }
    }

    public async Task<List<ConfigHistoryItemResponse>> GetHistoryAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/configs/history?namespace={Uri.EscapeDataString(@namespace)}&group={Uri.EscapeDataString(group)}&key={Uri.EscapeDataString(key)}";
        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Get, url),
            options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }

        var data = await HttpJson.ReadAsync<List<ConfigHistoryItemResponse>>(response.Content, cancellationToken);
        return data ?? new List<ConfigHistoryItemResponse>();
    }

    public async Task<ApiResult<ConfigItemResponse>> SubscribeAsync(string @namespace, string group, string key, int? timeoutMs = null, string? etag = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"namespace={Uri.EscapeDataString(@namespace)}",
            $"group={Uri.EscapeDataString(group)}",
            $"key={Uri.EscapeDataString(key)}"
        };

        if (timeoutMs.HasValue)
        {
            query.Add($"timeoutMs={timeoutMs.Value}");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/configs/subscribe?" + string.Join("&", query));
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        var client = _httpClientFactory.CreateClient("SiNan");
        var options = _options.Value;
        var response = await HttpRetry.SendAsync(
            client,
            () =>
            {
                var retryRequest = new HttpRequestMessage(HttpMethod.Get, request.RequestUri!);
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
            return new ApiResult<ConfigItemResponse>(response.StatusCode, null, responseEtag, true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }
        var payload = await HttpJson.ReadAsync<ConfigItemResponse>(response.Content, cancellationToken);
        return new ApiResult<ConfigItemResponse>(response.StatusCode, payload, responseEtag, false);
    }
}
