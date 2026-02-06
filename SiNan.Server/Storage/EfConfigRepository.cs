using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Storage;

public sealed class EfConfigRepository : IConfigRepository
{
    private readonly SiNanDbContext _dbContext;

    public EfConfigRepository(SiNanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ConfigItemEntity?> GetConfigAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default)
    {
        return _dbContext.ConfigItems
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Namespace == @namespace && c.Group == group && c.Key == key, cancellationToken);
    }

    public async Task<ConfigItemEntity> AddConfigAsync(ConfigItemEntity config, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.ConfigItems.AddAsync(config, cancellationToken);
        return entry.Entity;
    }

    public Task UpdateConfigAsync(ConfigItemEntity config, CancellationToken cancellationToken = default)
    {
        _dbContext.ConfigItems.Update(config);
        return Task.CompletedTask;
    }

    public Task DeleteConfigAsync(ConfigItemEntity config, CancellationToken cancellationToken = default)
    {
        _dbContext.ConfigItems.Remove(config);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ConfigHistoryEntity>> GetHistoryAsync(Guid configId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConfigHistory
            .AsNoTracking()
            .Where(h => h.ConfigId == configId)
            .OrderByDescending(h => h.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task AddHistoryAsync(ConfigHistoryEntity history, CancellationToken cancellationToken = default)
    {
        await _dbContext.ConfigHistory.AddAsync(history, cancellationToken);
    }

    public Task DeleteHistoryAsync(ConfigHistoryEntity history, CancellationToken cancellationToken = default)
    {
        _dbContext.ConfigHistory.Remove(history);
        return Task.CompletedTask;
    }
}
