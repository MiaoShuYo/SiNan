namespace SiNan.SDK.Registry;

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

public sealed class DeregisterInstanceRequest
{
    public string Namespace { get; set; } = "default";
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string ServiceName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
}

public sealed class HeartbeatRequest
{
    public string Namespace { get; set; } = "default";
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string ServiceName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
}

public sealed class RegisterInstanceResponse
{
    public string InstanceId { get; set; } = string.Empty;
    public Guid ServiceId { get; set; }
}

public sealed class HeartbeatResponse
{
    public string InstanceId { get; set; } = string.Empty;
}

public sealed class ServiceInstance
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

public sealed class ServiceInstancesResponse
{
    public string Namespace { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public List<ServiceInstance> Instances { get; set; } = new();
}
