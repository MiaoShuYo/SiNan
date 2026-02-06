using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Registry;

public sealed class RegistryHealthMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RegistryChangeNotifier _notifier;
    private readonly IOptions<RegistryHealthOptions> _options;
    private readonly ILogger<RegistryHealthMonitor> _logger;

    public RegistryHealthMonitor(
        IServiceScopeFactory scopeFactory,
        RegistryChangeNotifier notifier,
        IOptions<RegistryHealthOptions> options,
        ILogger<RegistryHealthMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _options.Value;
        if (!settings.Enabled)
        {
            _logger.LogInformation("Registry health monitor disabled.");
            return;
        }

        var intervalSeconds = Math.Clamp(settings.CheckIntervalSeconds, 1, 300);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ScanOnceAsync(settings, stoppingToken);
        }
    }

    private async Task ScanOnceAsync(RegistryHealthOptions settings, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SiNanDbContext>();

        var now = DateTimeOffset.UtcNow;
        var instances = await dbContext.ServiceInstances
            .Include(i => i.Service)
            .ToListAsync(cancellationToken);

        if (instances.Count == 0)
        {
            return;
        }

        var evictionGrace = TimeSpan.FromSeconds(Math.Max(0, settings.EvictionGraceSeconds));
        var touchedServices = new HashSet<string>(StringComparer.Ordinal);
        var changeCount = 0;

        foreach (var instance in instances)
        {
            var ttlSeconds = instance.TtlSeconds <= 0 ? 30 : instance.TtlSeconds;
            var staleAt = instance.LastHeartbeatAt.AddSeconds(ttlSeconds);

            if (now < staleAt)
            {
                continue;
            }

            var shouldEvict = instance.IsEphemeral && now >= staleAt.Add(evictionGrace);
            if (shouldEvict)
            {
                dbContext.ServiceInstances.Remove(instance);
                changeCount++;
                TouchService(instance.Service, now, touchedServices);
                continue;
            }

            if (!instance.Healthy)
            {
                continue;
            }

            instance.Healthy = false;
            instance.UpdatedAt = now;
            dbContext.ServiceInstances.Update(instance);
            changeCount++;
            TouchService(instance.Service, now, touchedServices);
        }

        if (changeCount == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var serviceKey in touchedServices)
        {
            _notifier.Notify(serviceKey);
        }
    }

    private static void TouchService(ServiceEntity? service, DateTimeOffset now, HashSet<string> touchedServices)
    {
        if (service is null)
        {
            return;
        }

        service.UpdatedAt = now;
        touchedServices.Add(BuildServiceKey(service.Namespace, service.Group, service.Name));
    }

    private static string BuildServiceKey(string @namespace, string group, string serviceName)
    {
        return $"{@namespace}::{group}::{serviceName}";
    }
}
