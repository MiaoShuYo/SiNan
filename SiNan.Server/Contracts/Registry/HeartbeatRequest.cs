namespace SiNan.Server.Contracts.Registry;

public sealed class HeartbeatRequest
{
    public string Namespace { get; set; } = "default";
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string ServiceName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? InstanceId { get; set; }
}
