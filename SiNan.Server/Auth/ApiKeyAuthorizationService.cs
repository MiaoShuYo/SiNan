using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SiNan.Server.Contracts.Common;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;
using SiNan.Server.Storage;

namespace SiNan.Server.Auth;

public sealed class ApiKeyAuthorizationService
{
    private readonly IOptions<ApiKeyAuthOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    // Lightweight in-memory cache to avoid hitting the DB on every API call.
    // Keyed by API key string. Invalidated on write operations (create/update/delete).
    private static readonly ConcurrentDictionary<string, ApiKeyEntity?> KeyCache = new(StringComparer.Ordinal);

    public ApiKeyAuthorizationService(IOptions<ApiKeyAuthOptions> options, IServiceScopeFactory scopeFactory)
    {
        _options = options;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Invalidate the in-memory cache for a given key (or all keys if null).
    /// Call this after creating, updating, or deleting an API key.
    /// </summary>
    public static void InvalidateCache(string? key = null)
    {
        if (key is null)
        {
            KeyCache.Clear();
            return;
        }

        KeyCache.TryRemove(key, out _);
    }

    /// <summary>
    /// Look up an API key from the database (with in-memory cache).
    /// Returns null if no matching active key is found.
    /// </summary>
    private ApiKeyEntity? GetKey(string token)
    {
        if (KeyCache.TryGetValue(token, out var cached))
        {
            return cached;
        }

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
        var key = repo.GetByKeyAsync(token).GetAwaiter().GetResult();

        // Cache for up to ~60 seconds via a simple approach:
        // we store it now and rely on InvalidateCache for writes.
        // For a production system, use IMemoryCache with sliding expiration.
        if (key is not null)
        {
            KeyCache[token] = key;
        }
        else
        {
            // Cache null result for 10s to avoid repeated DB hits for invalid keys.
            // This is a simple approach; a production implementation would use IMemoryCache.
            KeyCache[token] = null;
            // Schedule removal after a short time so a valid key added later takes effect.
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                KeyCache.TryRemove(token, out _);
            });
        }

        return key;
    }

    public AuthResult AuthorizeNamespaceGroup(HttpContext context, string @namespace, string group)
    {
        var options = _options.Value;
        var actorFromHeader = GetActorFromHeader(context, options);

        if (!options.Enabled)
        {
            var fallbackActor = string.IsNullOrWhiteSpace(actorFromHeader) ? "anonymous" : actorFromHeader;
            return AuthResult.Allow(fallbackActor);
        }

        if (!TryGetToken(context, options, out var token))
        {
            return AuthResult.Deny(ErrorCodes.Unauthorized, "Missing API token.", StatusCodes.Status401Unauthorized);
        }

        var key = GetKey(token);
        if (key is null)
        {
            return AuthResult.Deny(ErrorCodes.Unauthorized, "Invalid API token.", StatusCodes.Status401Unauthorized);
        }

        var namespaces = ApiKeyJson.DeserializeList(key.NamespacesJson);
        var groups = ApiKeyJson.DeserializeList(key.GroupsJson);

        if (namespaces.Count > 0 && !namespaces.Contains(@namespace, StringComparer.Ordinal))
        {
            return AuthResult.Deny(ErrorCodes.Forbidden, "Namespace is not allowed.", StatusCodes.Status403Forbidden);
        }

        if (groups.Count > 0 && !groups.Contains(group, StringComparer.Ordinal))
        {
            return AuthResult.Deny(ErrorCodes.Forbidden, "Group is not allowed.", StatusCodes.Status403Forbidden);
        }

        var actor = GetActor(key, actorFromHeader);
        return AuthResult.Allow(actor);
    }

    public AuthResult AuthorizeAction(HttpContext context, string @namespace, string group, string action, string resource)
    {
        var options = _options.Value;
        var actorFromHeader = GetActorFromHeader(context, options);

        if (!options.Enabled)
        {
            var fallbackActor = string.IsNullOrWhiteSpace(actorFromHeader) ? "anonymous" : actorFromHeader;
            return AuthResult.Allow(fallbackActor);
        }

        if (!TryGetToken(context, options, out var token))
        {
            return AuthResult.Deny(ErrorCodes.Unauthorized, "Missing API token.", StatusCodes.Status401Unauthorized);
        }

        var key = GetKey(token);
        if (key is null)
        {
            return AuthResult.Deny(ErrorCodes.Unauthorized, "Invalid API token.", StatusCodes.Status401Unauthorized);
        }

        // Admin keys bypass all permission checks
        if (key.IsAdmin)
        {
            var adminActor = GetActor(key, actorFromHeader);
            return AuthResult.Allow(adminActor);
        }

        var namespaces = ApiKeyJson.DeserializeList(key.NamespacesJson);
        var groups = ApiKeyJson.DeserializeList(key.GroupsJson);
        var actions = ApiKeyJson.DeserializeList(key.AllowedActionsJson);
        var resources = ApiKeyJson.DeserializeList(key.AllowedResourcesJson);

        if (namespaces.Count > 0 && !namespaces.Contains(@namespace, StringComparer.Ordinal))
        {
            return AuthResult.Deny(ErrorCodes.Forbidden, "Namespace is not allowed.", StatusCodes.Status403Forbidden);
        }

        if (groups.Count > 0 && !groups.Contains(group, StringComparer.Ordinal))
        {
            return AuthResult.Deny(ErrorCodes.Forbidden, "Group is not allowed.", StatusCodes.Status403Forbidden);
        }

        if (actions.Count > 0 && !actions.Contains(action, StringComparer.Ordinal))
        {
            return AuthResult.Deny(ErrorCodes.Forbidden, "Action is not allowed.", StatusCodes.Status403Forbidden);
        }

        if (resources.Count > 0 && !resources.Any(prefix => resource.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return AuthResult.Deny(ErrorCodes.Forbidden, "Resource is not allowed.", StatusCodes.Status403Forbidden);
        }

        var actor = GetActor(key, actorFromHeader);
        return AuthResult.Allow(actor);
    }

    public AuthResult AuthorizeAdmin(HttpContext context)
    {
        var options = _options.Value;
        var actorFromHeader = GetActorFromHeader(context, options);

        if (!options.Enabled)
        {
            var fallbackActor = string.IsNullOrWhiteSpace(actorFromHeader) ? "anonymous" : actorFromHeader;
            return AuthResult.Allow(fallbackActor);
        }

        if (!TryGetToken(context, options, out var token))
        {
            return AuthResult.Deny(ErrorCodes.Unauthorized, "Missing API token.", StatusCodes.Status401Unauthorized);
        }

        var key = GetKey(token);
        if (key is null)
        {
            return AuthResult.Deny(ErrorCodes.Unauthorized, "Invalid API token.", StatusCodes.Status401Unauthorized);
        }

        if (!key.IsAdmin)
        {
            return AuthResult.Deny(ErrorCodes.Forbidden, "Admin access required.", StatusCodes.Status403Forbidden);
        }

        var actor = GetActor(key, actorFromHeader);
        return AuthResult.Allow(actor);
    }

    private static string GetActorFromHeader(HttpContext context, ApiKeyAuthOptions options)
    {
        return context.Request.Headers.TryGetValue(options.ActorHeaderName, out var actorValue)
            ? actorValue.ToString()
            : string.Empty;
    }

    private static bool TryGetToken(HttpContext context, ApiKeyAuthOptions options, out string token)
    {
        if (context.Request.Headers.TryGetValue(options.HeaderName, out var tokenValue) && !string.IsNullOrWhiteSpace(tokenValue))
        {
            token = tokenValue.ToString();
            return true;
        }

        token = string.Empty;
        return false;
    }

    private static string GetActor(ApiKeyEntity key, string actorFromHeader)
    {
        return !string.IsNullOrWhiteSpace(key.Actor)
            ? key.Actor
            : string.IsNullOrWhiteSpace(actorFromHeader) ? "unknown" : actorFromHeader;
    }
}
