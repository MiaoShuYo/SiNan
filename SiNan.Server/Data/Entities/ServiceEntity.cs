using System;
using System.Collections.Generic;

namespace SiNan.Server.Data.Entities;

public sealed class ServiceEntity
{
    public Guid Id { get; set; }
    public string Namespace { get; set; } = "default";
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string Name { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ServiceInstanceEntity> Instances { get; set; } = new List<ServiceInstanceEntity>();
}
