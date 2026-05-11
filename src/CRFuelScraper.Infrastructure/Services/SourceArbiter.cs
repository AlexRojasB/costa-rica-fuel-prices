using System.Diagnostics;
using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Core.Options;
using CRFuelScraper.Infrastructure.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRFuelScraper.Infrastructure.Services;

/// <summary>
/// Manages a priority chain of three sources:
///   1. RECOPE API    (confidence 0.99)
///   2. RECOPE HTML   (confidence 0.92)
///   3. ARESEP        (confidence 0.85)
///   4. LastKnownGood (confidence 0.30, is_stale = true)
/// </summary>
public class SourceArbiter(
    RecopeFuelScraperService recopeHtml,
    RecopeApiScraperService  recopeApi,
    AresepFuelScraperService aresep,
    IFuelPriceRepository     repository,
    ISourceHealthRepository  healthRepo,
    ILogger<SourceArbiter>   logger,
    IOptions<ScrapingOptions> options) : ISourceArbiter
{
    private static readonly (string Name, int Priority, decimal Confidence)[] SourceMeta =
    [
        ("RECOPE API",  1, 0.99m),
        ("RECOPE HTML", 2, 0.92m),
        ("ARESEP",      3, 0.85m)
    ];

    private const decimal ConflictDegradePenalty   = 0.05m;
    private const decimal ConflictDegradeThreshold = 0.05m;

    public async Task<IReadOnlyList<FuelPrice>> GetPricesAsync(CancellationToken ct = default)
    {
        var opts          = options.Value;
        var lastKnownDate = await repository.GetLatestEffectiveDateAsync(ct);

        IFuelScraperService[] scrapers = [recopeApi, recopeHtml, aresep];

        for (int i = 0; i < scrapers.Length; i++)
        {
            var (sourceName, priority, confidence) = SourceMeta[i];
            var scraper = scrapers[i];
            var sw = Stopwatch.StartNew();

            try
            {
                var prices = await scraper.ScrapeAsync(ct);
                sw.Stop();

                if (prices.Count < opts.MinExpectedProducts)
                {
                    logger.LogWarning(
                        "Arbiter: {Source} returned only {Count}/{Min} prices — skipping",
                        sourceName, prices.Count, opts.MinExpectedProducts);

                    await RecordHealthAsync(sourceName, SourceStatus.Degraded,
                        (int)sw.ElapsedMilliseconds, effectiveDate: null,
                        error: $"Incomplete: {prices.Count}/{opts.MinExpectedProducts} prices", ct);
                    continue;
                }

                var effectiveDate = prices.Max(p => p.EffectiveDate);

                if (IsStaleByEffectiveDate(effectiveDate, lastKnownDate, opts.StalenessThresholdDays))
                {
                    var msg = $"Stale: effective {effectiveDate} is >{opts.StalenessThresholdDays} days behind last known {lastKnownDate}";
                    logger.LogWarning("Arbiter: {Source} {Msg} — skipping", sourceName, msg);

                    await RecordHealthAsync(sourceName, SourceStatus.Degraded,
                        (int)sw.ElapsedMilliseconds, effectiveDate, error: msg, ct);
                    continue;
                }

                var sourceUpdatedAt = prices
                    .Select(p => p.SourceUpdatedAt)
                    .FirstOrDefault(d => d.HasValue);

                if (sourceUpdatedAt.HasValue)
                {
                    var publishedAge = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber
                                      - sourceUpdatedAt.Value.DayNumber;
                    if (publishedAge > opts.MaxPublishedAgeDays)
                    {
                        var msg = $"Published date {sourceUpdatedAt.Value} is {publishedAge} days old (max {opts.MaxPublishedAgeDays})";
                        logger.LogWarning("Arbiter: {Source} {Msg} — skipping", sourceName, msg);

                        await RecordHealthAsync(sourceName, SourceStatus.Degraded,
                            (int)sw.ElapsedMilliseconds, effectiveDate, error: msg, ct);
                        continue;
                    }
                }

                await RecordHealthAsync(sourceName, SourceStatus.Healthy,
                    (int)sw.ElapsedMilliseconds, effectiveDate, error: null, ct);

                logger.LogInformation(
                    "Arbiter: {Source} (priority {P}) succeeded — {Count} prices, effective {Date}, {Ms}ms",
                    sourceName, priority, prices.Count, effectiveDate, sw.ElapsedMilliseconds);

                var enriched = EnrichPrices(prices, confidence, priority);

                if (i == 0)
                    await CrossValidateWithHtmlAsync(enriched, ct);

                return enriched;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.LogWarning(ex, "Arbiter: {Source} failed — trying next source", sourceName);
                await RecordHealthAsync(sourceName, SourceStatus.Down,
                    (int)sw.ElapsedMilliseconds, effectiveDate: null, error: ex.Message,
                    CancellationToken.None);
            }
        }

        return await LastKnownGoodAsync(ct);
    }

    private async Task CrossValidateWithHtmlAsync(List<FuelPrice> apiPrices, CancellationToken ct)
    {
        IReadOnlyList<FuelPrice> htmlPrices;
        try
        {
            htmlPrices = await recopeHtml.ScrapeAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Arbiter: cross-validation HTML scrape failed — skipping conflict check");
            return;
        }

        var htmlByType = htmlPrices
            .Where(p => p.EffectiveDate == apiPrices.Max(a => a.EffectiveDate))
            .ToDictionary(p => p.FuelType, p => p.Price);

        foreach (var apiPrice in apiPrices)
        {
            if (!htmlByType.TryGetValue(apiPrice.FuelType, out var htmlPrice)) continue;
            if (apiPrice.Price == htmlPrice) continue;

            var diff    = Math.Abs(apiPrice.Price - htmlPrice);
            var diffPct = diff / apiPrice.Price;

            apiPrice.HasConflict = true;
            apiPrice.ConflictNote =
                $"RECOPE API={apiPrice.Price:F2}, RECOPE HTML={htmlPrice:F2} para {apiPrice.EffectiveDate:yyyy-MM-dd}";

            if (diffPct > ConflictDegradeThreshold)
                apiPrice.ConfidenceScore -= ConflictDegradePenalty;

            logger.LogWarning(
                "Arbiter: conflict on {FuelType} — API={ApiPrice}, HTML={HtmlPrice} ({DiffPct:P1} diff)",
                apiPrice.FuelType, apiPrice.Price, htmlPrice, diffPct);
        }
    }

    private static bool IsStaleByEffectiveDate(DateOnly scrapedDate, DateOnly? lastKnownDate, int thresholdDays)
        => lastKnownDate.HasValue
           && (lastKnownDate.Value.DayNumber - scrapedDate.DayNumber) > thresholdDays;

    private static List<FuelPrice> EnrichPrices(
        IReadOnlyList<FuelPrice> prices, decimal confidence, int priority)
    {
        foreach (var p in prices)
        {
            p.ConfidenceScore = confidence;
            p.SourcePriority  = priority;
            p.IsStale         = false;
            p.ContentHash     = ContentHasher.Compute(p.CanonicalCode, p.Price, p.EffectiveDate, p.Source);
        }
        return [.. prices];
    }

    private async Task<IReadOnlyList<FuelPrice>> LastKnownGoodAsync(CancellationToken ct)
    {
        var prices = await repository.GetLatestPricesAsync(ct);

        if (prices.Count == 0)
        {
            logger.LogError("Arbiter: LastKnownGood has no data — database is empty");
            return [];
        }

        logger.LogWarning(
            "Arbiter: serving LastKnownGood ({Count} prices, effective {Date}) — confidence 0.30",
            prices.Count, prices.Max(p => p.EffectiveDate));

        foreach (var p in prices)
        {
            p.IsStale         = true;
            p.ConfidenceScore = 0.30m;
            p.SourcePriority  = 99;
        }
        return prices;
    }

    private Task RecordHealthAsync(
        string sourceName, SourceStatus status, int responseMs,
        DateOnly? effectiveDate, string? error, CancellationToken ct)
        => healthRepo.RecordAsync(new SourceHealth
        {
            SourceName    = sourceName,
            CheckedAt     = DateTime.UtcNow,
            Status        = status,
            ResponseMs    = responseMs,
            EffectiveDate = effectiveDate,
            ErrorMessage  = error
        }, ct);
}
