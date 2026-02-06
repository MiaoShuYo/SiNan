using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace SiNan.Server.Auth;

public sealed class ApiKeyAuthorizationService
{
    private readonly IOptions<ApiKeyAuthOptions> _options;

    public ApiKeyAuthorizationService(IOptions<ApiKeyAuthOptions> options)
    {
        _options = options;
    }

    public AuthResult AuthorizeNamespaceGroup(HttpContext context, string @namespace, string group)
    {
        var options = _options.Value;
        var actorHeader = options.ActorHeaderName;
        var actorFromHeader = context.Request.Headers.TryGetValue(actorHeader, out var actorValue)
            ? actorValue.ToString()
            : string.Empty;

        if (!options.Enabled)
        {
            var actor = string.IsNullOrWhiteSpace(actorFromHeader) ? "anonymous" : actorFromHeader;
            return AuthResult.Allow(actor);
        }

        if (!context.Request.Headers.TryGetValue(options.HeaderName, out var tokenValue) || string.IsNullOrWhiteSpace(tokenValue))
        {
            return AuthResult.Deny("unauthorized", "Missing API token.", StatusCodes.Status401Unauthorized);
        }

        var token = tokenValue.ToString();
        var key = options.ApiKeys.FirstOrDefault(item => string.Equals(item.Key, token, StringComparison.Ordinal));
        if (key is null)
        {
            return AuthResult.Deny("unauthorized", "Invalid API token.", StatusCodes.Status401Unauthorized);
        }

        if (key.Namespaces.Count > 0 && !key.Namespaces.Contains(@namespace, StringComparer.Ordinal))
        {
            return AuthResult.Deny("forbidden", "Namespace is not allowed.", StatusCodes.Status403Forbidden);
        }

        if (key.Groups.Count > 0 && !key.Groups.Contains(group, StringComparer.Ordinal))
        {
            return AuthResult.Deny("forbidden", "Group is not allowed.", StatusCodes.Status403Forbidden);
        }

        var actor = !string.IsNullOrWhiteSpace(key.Actor)
            ? key.Actor
            : string.IsNullOrWhiteSpace(actorFromHeader) ? "unknown" : actorFromHeader;

        return AuthResult.Allow(actor!);
    }
}
