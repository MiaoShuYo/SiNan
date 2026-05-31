namespace SiNan.Server.Contracts.ApiKeys;

public sealed class ApiKeyUpdateRequest
{
    public string Actor { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool Enabled { get; set; } = true;
    public string[] Namespaces { get; set; } = [];
    public string[] Groups { get; set; } = [];
    public string[] AllowedActions { get; set; } = [];
    public string[] AllowedResources { get; set; } = [];
}
