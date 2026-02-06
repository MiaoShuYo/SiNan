using System;

namespace SiNan.Server.Contracts.Config;

public sealed class ConfigListItemResponse
{
    public string Namespace { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
