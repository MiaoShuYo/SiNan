using System;

namespace SiNan.Server.Contracts.Registry;

public sealed class ServiceSummaryResponse
{
    public string Namespace { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int InstanceCount { get; set; }
    public int HealthyInstanceCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
