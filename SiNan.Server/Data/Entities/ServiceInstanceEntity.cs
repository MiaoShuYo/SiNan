using System;

namespace SiNan.Server.Data.Entities;

public sealed class ServiceInstanceEntity
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public int Weight { get; set; } = 100;
    public bool Healthy { get; set; } = true;
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset LastHeartbeatAt { get; set; }
    public int TtlSeconds { get; set; } = 30;
    public bool IsEphemeral { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ServiceEntity? Service { get; set; }
}
