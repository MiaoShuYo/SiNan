namespace SiNan.Server.Contracts.Config;

public sealed class ConfigUpsertRequest
{
    public string Namespace { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? PublishedBy { get; set; }
}
