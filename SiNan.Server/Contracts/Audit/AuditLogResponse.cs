using System;

namespace SiNan.Server.Contracts.Audit;

public sealed class AuditLogResponse
{
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
