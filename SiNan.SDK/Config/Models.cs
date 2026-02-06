namespace SiNan.SDK.Config;

public sealed class ConfigUpsertRequest
{
    public string Namespace { get; set; } = "default";
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? PublishedBy { get; set; }
}

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

public sealed class ConfigHistoryItemResponse
{
    public int Version { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
}
