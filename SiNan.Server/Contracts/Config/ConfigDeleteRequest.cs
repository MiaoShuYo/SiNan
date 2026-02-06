namespace SiNan.Server.Contracts.Config;

public sealed class ConfigDeleteRequest
{
    public string Namespace { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}
