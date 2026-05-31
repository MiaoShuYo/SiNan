using System.Collections.Generic;

namespace SiNan.Server.Auth;

public sealed class ApiKeyAuthOptions
{
    public bool Enabled { get; set; } = false;
    public string HeaderName { get; set; } = "X-SiNan-Token";
    public string ActorHeaderName { get; set; } = "X-SiNan-Actor";
    /// <summary>
    /// Bootstrap admin key. On first startup when the database has no API keys,
    /// an admin key with this value will be auto-created. Leave empty to skip.
    /// </summary>
    public string BootstrapAdminKey { get; set; } = string.Empty;
}
