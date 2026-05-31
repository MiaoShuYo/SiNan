using System;

namespace SiNan.Server.Contracts.ApiKeys;

public sealed class ApiKeyResponse
{
    public Guid Id { get; set; }
    /// <summary>Masked key — only first 4 and last 4 characters shown.</summary>
    public string KeyMasked { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string[] Namespaces { get; set; } = [];
    public string[] Groups { get; set; } = [];
    public string[] AllowedActions { get; set; } = [];
    public string[] AllowedResources { get; set; } = [];
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
