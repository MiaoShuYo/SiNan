using System.Net;

namespace SiNan.SDK;

public sealed class ApiException : Exception
{
    public ApiException(string message, HttpStatusCode statusCode, string? errorCode, string? traceId, object? details)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        TraceId = traceId;
        Details = details;
    }

    public HttpStatusCode StatusCode { get; }
    public string? ErrorCode { get; }
    public string? TraceId { get; }
    public object? Details { get; }

    public static async Task<ApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await HttpJson.ReadAsync<ErrorResponse>(response.Content, cancellationToken);
        if (payload is not null)
        {
            return new ApiException(payload.Message, response.StatusCode, payload.Code, payload.TraceId, payload.Details);
        }

        var message = response.ReasonPhrase ?? "Request failed.";
        return new ApiException(message, response.StatusCode, null, null, null);
    }
}
