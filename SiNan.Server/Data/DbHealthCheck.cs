using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SiNan.Server.Data;

/// <summary>
/// Health check that verifies database connectivity by opening a real connection.
/// Used by Docker/Compose healthcheck to detect when the database becomes available.
/// </summary>
internal sealed class DbHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DbHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SiNanDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable")
                : HealthCheckResult.Unhealthy("Cannot connect to database");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to database", ex);
        }
    }
}
