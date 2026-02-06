namespace SiNan.SDK.Config;

public interface ISiNanConfigClient
{
    Task<ConfigItemResponse> CreateAsync(ConfigUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ConfigItemResponse> UpdateAsync(ConfigUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ConfigItemResponse> GetAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default);
    Task<ConfigItemResponse> RollbackAsync(string @namespace, string group, string key, int version, string? publishedBy = null, CancellationToken cancellationToken = default);
    Task<List<ConfigHistoryItemResponse>> GetHistoryAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default);
    Task<ApiResult<ConfigItemResponse>> SubscribeAsync(string @namespace, string group, string key, int? timeoutMs = null, string? etag = null, CancellationToken cancellationToken = default);
}
