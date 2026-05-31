using System;

namespace SiNan.Server.Data.Entities;

/// <summary>
/// Represents an API key used for server-to-server authentication.
/// Each key has an actor identity, admin flag, and scoped permissions.
/// </summary>
public sealed class ApiKeyEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// The API key string (bearer token).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable identity associated with this key (e.g., "admin", "billing-service").
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// When true, this key bypasses all action/resource checks and has full access.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// JSON array of allowed namespaces. Empty array = allow all.
    /// </summary>
    public string NamespacesJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of allowed groups. Empty array = allow all.
    /// </summary>
    public string GroupsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of allowed actions. Empty array = allow all.
    /// </summary>
    public string AllowedActionsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of allowed resource prefixes. Empty array = allow all.
    /// </summary>
    public string AllowedResourcesJson { get; set; } = "[]";

    /// <summary>
    /// Whether this key is currently active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
