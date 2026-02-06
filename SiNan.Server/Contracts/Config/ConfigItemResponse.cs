using System;

namespace SiNan.Server.Contracts.Config;

public sealed class ConfigItemResponse
{
    public string Namespace { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
