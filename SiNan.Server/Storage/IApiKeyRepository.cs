using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Storage;

public interface IApiKeyRepository
{
    Task<IReadOnlyList<ApiKeyEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ApiKeyEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiKeyEntity?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<ApiKeyEntity> AddAsync(ApiKeyEntity apiKey, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApiKeyEntity apiKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(ApiKeyEntity apiKey, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
