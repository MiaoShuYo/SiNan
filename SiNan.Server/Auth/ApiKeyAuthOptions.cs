using System.Collections.Generic;

namespace SiNan.Server.Auth;

public sealed class ApiKeyAuthOptions
{
    public bool Enabled { get; set; } = false;
    public string HeaderName { get; set; } = "X-SiNan-Token";
    public string ActorHeaderName { get; set; } = "X-SiNan-Actor";
    public List<ApiKeyDefinition> ApiKeys { get; set; } = new();
}

public sealed class ApiKeyDefinition
{
    public string Key { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public List<string> Namespaces { get; set; } = new();
    public List<string> Groups { get; set; } = new();
}
