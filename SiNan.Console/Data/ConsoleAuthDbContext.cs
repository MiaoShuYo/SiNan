using Microsoft.EntityFrameworkCore;

namespace SiNan.Console.Data;

public sealed class ConsoleAuthDbContext : DbContext
{
    public ConsoleAuthDbContext(DbContextOptions<ConsoleAuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuthUser> Users => Set<AuthUser>();
}
