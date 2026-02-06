using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Storage;

public interface IConfigRepository
{
    Task<ConfigItemEntity?> GetConfigAsync(string @namespace, string group, string key, CancellationToken cancellationToken = default);
    Task<ConfigItemEntity> AddConfigAsync(ConfigItemEntity config, CancellationToken cancellationToken = default);
    Task UpdateConfigAsync(ConfigItemEntity config, CancellationToken cancellationToken = default);
    Task DeleteConfigAsync(ConfigItemEntity config, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfigHistoryEntity>> GetHistoryAsync(Guid configId, CancellationToken cancellationToken = default);
    Task AddHistoryAsync(ConfigHistoryEntity history, CancellationToken cancellationToken = default);
    Task DeleteHistoryAsync(ConfigHistoryEntity history, CancellationToken cancellationToken = default);
}
