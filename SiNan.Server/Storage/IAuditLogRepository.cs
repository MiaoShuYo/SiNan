using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Storage;

public interface IAuditLogRepository
{
    Task AddAuditAsync(AuditLogEntity auditLog, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntity>> QueryAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntity>> QueryAsync(
        int take,
        string? action,
        string? resource,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);
}
