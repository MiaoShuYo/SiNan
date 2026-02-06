using System.Net;

namespace SiNan.SDK.Config;

public sealed class SiNanConfigClient : ISiNanConfigClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SiNanConfigClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ConfigItemResponse> CreateAsync(ConfigUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await HttpJson.PostAsync(client, "/api/v1/configs", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await HttpJson.ReadAsync<ConfigItemResponse>(response.Content, cancellationToken))!;
    }

    public async Task<ConfigItemResponse> UpdateAsync(ConfigUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await HttpJson.PutAsync(client, "/api/v1/configs", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await HttpJson.ReadAsync<ConfigItemResponse>(response.Content, cancellationToken))!;
    }

    public async Task<ConfigItemResponse> GetAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/configs?namespace={Uri.EscapeDataString(@namespace)}&group={Uri.EscapeDataString(group)}&key={Uri.EscapeDataString(key)}";
        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await HttpJson.ReadAsync<ConfigItemResponse>(response.Content, cancellationToken))!;
    }

    public async Task DeleteAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/configs?namespace={Uri.EscapeDataString(@namespace)}&group={Uri.EscapeDataString(group)}&key={Uri.EscapeDataString(key)}";
        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await client.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<ConfigHistoryItemResponse>> GetHistoryAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/configs/history?namespace={Uri.EscapeDataString(@namespace)}&group={Uri.EscapeDataString(group)}&key={Uri.EscapeDataString(key)}";
        var client = _httpClientFactory.CreateClient("SiNan");
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
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
        var response = await client.SendAsync(request, cancellationToken);
        var responseEtag = response.Headers.ETag?.Tag;

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new ApiResult<ConfigItemResponse>(response.StatusCode, null, responseEtag, true);
        }

        response.EnsureSuccessStatusCode();
        var payload = await HttpJson.ReadAsync<ConfigItemResponse>(response.Content, cancellationToken);
        return new ApiResult<ConfigItemResponse>(response.StatusCode, payload, responseEtag, false);
    }
}
