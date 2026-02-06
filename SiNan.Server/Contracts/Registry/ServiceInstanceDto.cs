using System.Collections.Generic;

namespace SiNan.Server.Contracts.Registry;

public sealed class ServiceInstanceDto
{
    public string InstanceId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public int Weight { get; set; }
    public bool Healthy { get; set; }
    public int TtlSeconds { get; set; }
    public bool IsEphemeral { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
