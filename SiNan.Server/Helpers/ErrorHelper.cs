using Microsoft.AspNetCore.Mvc;
using SiNan.Server.Contracts.Common;

namespace SiNan.Server.Helpers;

public static class ErrorHelper
{
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
