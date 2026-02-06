using System.Text.Json;
using Microsoft.Extensions.Options;
using SiNan.Server.Contracts.Common;

namespace SiNan.Server.Auth;

public sealed class ApiKeyAuthMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly IOptions<ApiKeyAuthOptions> _options;

    public ApiKeyAuthMiddleware(RequestDelegate next, IOptions<ApiKeyAuthOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            await _next(context);
            return;
        }

        if (!ShouldRequireToken(context.Request))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue(options.HeaderName, out var token) && !string.IsNullOrWhiteSpace(token))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var payload = new ErrorResponse
        {
            Code = ErrorCodes.Unauthorized,
            Message = "Missing API token.",
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static bool ShouldRequireToken(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.Path.StartsWithSegments("/api/v1/audit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Method is "POST" or "PUT" or "PATCH" or "DELETE";
    }
}
