using System.Threading;
using System.Threading.Tasks;

namespace SiNan.Server.Storage;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
