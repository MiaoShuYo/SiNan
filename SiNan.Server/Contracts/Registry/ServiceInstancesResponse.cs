using System.Collections.Generic;

namespace SiNan.Server.Contracts.Registry;

public sealed class ServiceInstancesResponse
{
    public string Namespace { get; set; } = "default";
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string ServiceName { get; set; } = string.Empty;
    public string? ETag { get; set; }
    public List<ServiceInstanceDto> Instances { get; set; } = new();
}
