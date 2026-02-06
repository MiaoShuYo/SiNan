using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Storage;

public sealed class EfAuditLogRepository : IAuditLogRepository
{
    private readonly SiNanDbContext _dbContext;

    public EfAuditLogRepository(SiNanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAuditAsync(AuditLogEntity auditLog, CancellationToken cancellationToken = default)
    {
        await _dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntity>> QueryAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntity>> QueryAsync(
        int take,
        string? action,
        string? resource,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLogEntity> query = _dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(resource))
        {
            query = query.Where(log => log.Resource.Contains(resource));
        }

        if (from.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= to.Value);
        }

        return await query
            .OrderByDescending(log => log.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
