using System.Net;

namespace SiNan.SDK;

public sealed class ApiResult<T>
{
    public ApiResult(HttpStatusCode statusCode, T? value, string? etag, bool notModified)
    {
        StatusCode = statusCode;
        Value = value;
        ETag = etag;
        NotModified = notModified;
    }

    public HttpStatusCode StatusCode { get; }
    public T? Value { get; }
    public string? ETag { get; }
    public bool NotModified { get; }
}
