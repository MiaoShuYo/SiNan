using System;
using System.Collections.Generic;

namespace SiNan.Server.Data.Entities;

public sealed class ConfigItemEntity
{
    public Guid Id { get; set; }
    public string Namespace { get; set; } = "default";
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/plain";
    public int Version { get; set; } = 1;
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ConfigHistoryEntity> History { get; set; } = new List<ConfigHistoryEntity>();
}
