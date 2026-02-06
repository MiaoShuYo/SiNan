using System.Collections.Generic;

namespace SiNan.Server.Contracts.Registry;

public sealed class RegisterInstanceRequest
{
    public string Namespace { get; set; } = "default";
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string ServiceName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public int Weight { get; set; } = 100;
    public int TtlSeconds { get; set; } = 30;
    public bool IsEphemeral { get; set; } = true;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
