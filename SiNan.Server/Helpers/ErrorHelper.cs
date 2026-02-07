/// <summary>
/// Error response helper class
/// Provides unified error response creation methods
/// </summary>

using Microsoft.AspNetCore.Mvc;
using SiNan.Server.Contracts.Common;

namespace SiNan.Server.Helpers;

public static class ErrorHelper
{
    /// <summary>
    /// Creates a unified error response
    /// </summary>
    /// <param name="context">HTTP context for obtaining trace identifier</param>
    /// <param name="code">Error code</param>
    /// <param name="message">Error message</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="details">Additional details (optional)</param>
    /// <returns>JSON formatted error response</returns>
    public static IActionResult CreateError(HttpContext context, string code, string message, int statusCode, object? details = null)
    {
        return new JsonResult(new ErrorResponse
        {
            Code = code,
            Message = message,
            Details = details,
            TraceId = context.TraceIdentifier
        })
        {
            StatusCode = statusCode
        };
    }
}
