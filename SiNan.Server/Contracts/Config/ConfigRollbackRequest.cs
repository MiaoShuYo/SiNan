namespace SiNan.Server.Contracts.Config;

public sealed class ConfigRollbackRequest
{
    public string Namespace { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? PublishedBy { get; set; }
}
