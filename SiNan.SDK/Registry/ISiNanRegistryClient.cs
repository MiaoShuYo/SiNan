namespace SiNan.SDK.Registry;

public interface ISiNanRegistryClient
{
    Task<RegisterInstanceResponse> RegisterAsync(RegisterInstanceRequest request, CancellationToken cancellationToken = default);
    Task DeregisterAsync(DeregisterInstanceRequest request, CancellationToken cancellationToken = default);
    Task<HeartbeatResponse> HeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<ServiceInstancesResponse>> GetInstancesAsync(string @namespace, string group, string serviceName, bool healthyOnly = true, string? etag = null, CancellationToken cancellationToken = default);
    Task<ApiResult<ServiceInstancesResponse>> SubscribeAsync(string @namespace, string group, string serviceName, bool healthyOnly = true, int? timeoutMs = null, string? etag = null, CancellationToken cancellationToken = default);
}
