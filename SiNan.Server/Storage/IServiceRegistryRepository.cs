using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Storage;

public interface IServiceRegistryRepository
{
    Task<ServiceEntity?> GetServiceAsync(string @namespace, string group, string name, CancellationToken cancellationToken = default);
    Task<ServiceEntity> AddServiceAsync(ServiceEntity service, CancellationToken cancellationToken = default);
    Task UpdateServiceAsync(ServiceEntity service, CancellationToken cancellationToken = default);
    Task DeleteServiceAsync(ServiceEntity service, CancellationToken cancellationToken = default);

    Task<ServiceInstanceEntity?> GetInstanceAsync(Guid serviceId, string host, int port, CancellationToken cancellationToken = default);
    Task AddInstanceAsync(ServiceInstanceEntity instance, CancellationToken cancellationToken = default);
    Task UpdateInstanceAsync(ServiceInstanceEntity instance, CancellationToken cancellationToken = default);
    Task DeleteInstanceAsync(ServiceInstanceEntity instance, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceInstanceEntity>> ListInstancesAsync(Guid serviceId, bool? healthyOnly, CancellationToken cancellationToken = default);
}
