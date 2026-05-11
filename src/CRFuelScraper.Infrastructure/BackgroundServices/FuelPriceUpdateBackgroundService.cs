using CRFuelScraper.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRFuelScraper.Infrastructure.BackgroundServices;

/// <summary>
/// Runs once on startup and then every day at 07:00 UTC to refresh fuel prices.
/// </summary>
public sealed class FuelPriceUpdateBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<FuelPriceUpdateBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay   = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FuelPriceUpdateBackgroundService starting");

        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunScrapeAsync(stoppingToken);

            var nextRun = GetNextRunTime();
            var delay   = nextRun - DateTime.UtcNow;
            if (delay < TimeSpan.Zero) delay = TimeSpan.FromMinutes(1);

            logger.LogInformation("Next price refresh scheduled at {NextRun} UTC (in {Delay:hh\\:mm})",
                nextRun, delay);

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunScrapeAsync(CancellationToken ct)
    {
        try
        {
            using var scope   = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IFuelPriceService>();

            logger.LogInformation("Running scheduled fuel price refresh");
            var result = await service.RefreshPricesAsync(ct);

            if (result.Updated)
                logger.LogInformation(
                    "Fuel prices updated — source={Source}, prices={Count}, effectiveDate={Date}, conflicts={Conflicts}, duration={Ms}ms",
                    result.DataSource, result.PricesUpdated, result.EffectiveDate, result.ConflictsFound, result.DurationMs);
            else
                logger.LogInformation("No new fuel prices found (already up to date)");
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled fuel price refresh failed — will retry in {Delay}", RetryDelay);
            await Task.Delay(RetryDelay, ct);
        }
    }

    // Runs once per day at 07:00 UTC (04:00 AM Costa Rica time)
    private static DateTime GetNextRunTime()
    {
        var now     = DateTime.UtcNow;
        var nextRun = now.Date.AddHours(7);
        if (now >= nextRun) nextRun = nextRun.AddDays(1);
        return nextRun;
    }
}
