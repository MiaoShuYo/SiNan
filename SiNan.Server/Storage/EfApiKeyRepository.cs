using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Storage;

public sealed class EfApiKeyRepository : IApiKeyRepository
{
    private readonly SiNanDbContext _dbContext;

    public EfApiKeyRepository(SiNanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ApiKeyEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiKeys
            .AsNoTracking()
            .OrderBy(k => k.Actor)
            .ThenBy(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<ApiKeyEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    public Task<ApiKeyEntity?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key && k.Enabled, cancellationToken);
    }

    public async Task<ApiKeyEntity> AddAsync(ApiKeyEntity apiKey, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.ApiKeys.AddAsync(apiKey, cancellationToken);
        return entry.Entity;
    }

    public Task UpdateAsync(ApiKeyEntity apiKey, CancellationToken cancellationToken = default)
    {
        _dbContext.ApiKeys.Update(apiKey);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ApiKeyEntity apiKey, CancellationToken cancellationToken = default)
    {
        _dbContext.ApiKeys.Remove(apiKey);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiKeys.CountAsync(cancellationToken);
    }
}
