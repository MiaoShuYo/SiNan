using System;
using System.Text.Json;
using SiNan.Server.Data.Entities;
using SiNan.Server.Storage;

namespace SiNan.Server.Audit;

public sealed class AuditLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAuditLogRepository _repository;

    public AuditLogWriter(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task AddAsync(
        string actor,
        string action,
        string resource,
        object? before,
        object? after,
        string traceId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var auditLog = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            Actor = actor,
            Action = action,
            Resource = resource,
            BeforeJson = Serialize(before),
            AfterJson = Serialize(after),
            TraceId = traceId,
            CreatedAt = now
        };

        await _repository.AddAuditAsync(auditLog, cancellationToken);
    }

    private static string? Serialize(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }
}
