using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Storage;

public sealed class EfServiceRegistryRepository : IServiceRegistryRepository
{
    private readonly SiNanDbContext _dbContext;

    public EfServiceRegistryRepository(SiNanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ServiceEntity?> GetServiceAsync(string @namespace, string group, string name, CancellationToken cancellationToken = default)
    {
        return _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Namespace == @namespace && s.Group == group && s.Name == name, cancellationToken);
    }

    public async Task<ServiceEntity> AddServiceAsync(ServiceEntity service, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.Services.AddAsync(service, cancellationToken);
        return entry.Entity;
    }

    public Task UpdateServiceAsync(ServiceEntity service, CancellationToken cancellationToken = default)
    {
        _dbContext.Services.Update(service);
        return Task.CompletedTask;
    }

    public Task DeleteServiceAsync(ServiceEntity service, CancellationToken cancellationToken = default)
    {
        _dbContext.Services.Remove(service);
        return Task.CompletedTask;
    }

    public Task<ServiceInstanceEntity?> GetInstanceAsync(Guid serviceId, string host, int port, CancellationToken cancellationToken = default)
    {
        return _dbContext.ServiceInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ServiceId == serviceId && i.Host == host && i.Port == port, cancellationToken);
    }

    public async Task AddInstanceAsync(ServiceInstanceEntity instance, CancellationToken cancellationToken = default)
    {
        await _dbContext.ServiceInstances.AddAsync(instance, cancellationToken);
    }

    public Task UpdateInstanceAsync(ServiceInstanceEntity instance, CancellationToken cancellationToken = default)
    {
        _dbContext.ServiceInstances.Update(instance);
        return Task.CompletedTask;
    }

    public Task DeleteInstanceAsync(ServiceInstanceEntity instance, CancellationToken cancellationToken = default)
    {
        _dbContext.ServiceInstances.Remove(instance);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ServiceInstanceEntity>> ListInstancesAsync(Guid serviceId, bool? healthyOnly, CancellationToken cancellationToken = default)
    {
        IQueryable<ServiceInstanceEntity> query = _dbContext.ServiceInstances
            .AsNoTracking()
            .Where(i => i.ServiceId == serviceId);

        if (healthyOnly.HasValue)
        {
            query = query.Where(i => i.Healthy == healthyOnly.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
