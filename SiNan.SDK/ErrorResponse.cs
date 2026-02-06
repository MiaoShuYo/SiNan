using System.Text.Json;

namespace SiNan.SDK;

public sealed class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public JsonElement? Details { get; set; }
    public string TraceId { get; set; } = string.Empty;
}
