using System;

namespace SiNan.Server.Contracts.Config;

public sealed class ConfigHistoryResponse
{
    public int Version { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
}
