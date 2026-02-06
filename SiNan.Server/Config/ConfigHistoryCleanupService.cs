using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiNan.Server.Data;

namespace SiNan.Server.Config;

public sealed class ConfigHistoryCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ConfigHistoryCleanupOptions> _options;
    private readonly ILogger<ConfigHistoryCleanupService> _logger;

    public ConfigHistoryCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<ConfigHistoryCleanupOptions> options,
        ILogger<ConfigHistoryCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _options.Value;
        if (!settings.Enabled)
        {
            _logger.LogInformation("Config history cleanup disabled.");
            return;
        }

        var intervalMinutes = Math.Clamp(settings.CheckIntervalMinutes, 1, 1440);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupOnceAsync(settings, stoppingToken);
        }
    }

    private async Task CleanupOnceAsync(ConfigHistoryCleanupOptions settings, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SiNanDbContext>();

        var historyItems = await dbContext.ConfigHistory
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (historyItems.Count == 0)
        {
            return;
        }

        var maxVersions = Math.Max(1, settings.MaxVersionsPerKey);
        var maxAgeDays = settings.MaxAgeDays;
        var threshold = maxAgeDays > 0 ? DateTimeOffset.UtcNow.AddDays(-maxAgeDays) : (DateTimeOffset?)null;
        var removeIds = new HashSet<Guid>();

        foreach (var group in historyItems.GroupBy(item => item.ConfigId))
        {
            var ordered = group.OrderByDescending(item => item.Version).ToList();

            for (var index = maxVersions; index < ordered.Count; index++)
            {
                removeIds.Add(ordered[index].Id);
            }

            if (threshold.HasValue)
            {
                foreach (var item in ordered)
                {
                    if (item.PublishedAt < threshold.Value)
                    {
                        removeIds.Add(item.Id);
                    }
                }
            }
        }

        if (removeIds.Count == 0)
        {
            return;
        }

        var toRemove = await dbContext.ConfigHistory
            .Where(item => removeIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (toRemove.Count == 0)
        {
            return;
        }

        dbContext.ConfigHistory.RemoveRange(toRemove);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Config history cleanup removed {Count} entries.", toRemove.Count);
    }
}
