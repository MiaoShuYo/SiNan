using System.Threading;
using System.Threading.Tasks;
using SiNan.Server.Data;

namespace SiNan.Server.Storage;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly SiNanDbContext _dbContext;

    public EfUnitOfWork(SiNanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
