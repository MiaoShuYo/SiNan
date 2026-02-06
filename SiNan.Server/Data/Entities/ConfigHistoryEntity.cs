using System;

namespace SiNan.Server.Data.Entities;

public sealed class ConfigHistoryEntity
{
    public Guid Id { get; set; }
    public Guid ConfigId { get; set; }
    public int Version { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/plain";
    public DateTimeOffset PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ConfigItemEntity? Config { get; set; }
}
