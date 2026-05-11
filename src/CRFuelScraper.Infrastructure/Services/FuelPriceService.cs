using System.Diagnostics;
using CRFuelScraper.Core.DTOs;
using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Core.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRFuelScraper.Infrastructure.Services;

public class FuelPriceService(
    IFuelPriceRepository repository,
    ISourceArbiter arbiter,
    IScraperLogRepository scraperLogs,
    IMemoryCache cache,
    ILogger<FuelPriceService> logger,
    IOptions<ScrapingOptions> options) : IFuelPriceService
{
    private const string LatestCacheKey    = "fuel:latest";
    private const string LatestAllCacheKey = "fuel:latest:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private const string Disclaimer =
        "Precios oficiales regulados por ARESEP y publicados por RECOPE. " +
        "Esta API es un proxy de datos públicos. " +
        "Verificar en fuente oficial antes de tomar decisiones financieras.";

    private static readonly Dictionary<FuelType, string> FuelTypeLabels = new()
    {
        [FuelType.Super]    = "Gasolina Súper",
        [FuelType.Regular]  = "Gasolina Regular",
        [FuelType.Diesel]   = "Diésel",
        [FuelType.Kerosene] = "Kerosene"
    };

    private static readonly HashSet<FuelType> PublicFuelTypes =
        [FuelType.Super, FuelType.Regular, FuelType.Diesel];

    private static readonly Dictionary<string, FuelType> CanonicalToFuelType =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["super"]    = FuelType.Super,
            ["regular"]  = FuelType.Regular,
            ["diesel"]   = FuelType.Diesel,
            ["kerosene"] = FuelType.Kerosene
        };

    public async Task<FuelLatestResponse> GetLatestPricesAsync(
        bool includeAll = false, CancellationToken ct = default)
    {
        var cacheKey = includeAll ? LatestAllCacheKey : LatestCacheKey;

        if (cache.TryGetValue(cacheKey, out FuelLatestResponse? cached) && cached is not null)
        {
            logger.LogDebug("Returning latest prices from cache (includeAll={IncludeAll})", includeAll);
            return cached with { FromCache = true };
        }

        var prices   = await repository.GetLatestPricesAsync(ct);
        var response = MapToLatestResponse(prices, fromCache: false, includeAll: includeAll);

        cache.Set(cacheKey, response, CacheDuration);
        return response;
    }

    public async Task<FuelPriceDto?> GetPriceByCanonicalCodeAsync(
        string canonicalCode, CancellationToken ct = default)
    {
        if (!CanonicalToFuelType.TryGetValue(canonicalCode, out var fuelType))
            return null;

        var price = await repository.GetLatestByFuelTypeAsync(fuelType, ct);
        return price is null ? null : MapPrice(price);
    }

    public async Task<PaginatedResponse<FuelHistoryResponse>> GetHistoryAsync(
        FuelHistoryRequest request, CancellationToken ct = default)
    {
        var paginated = await repository.GetHistoryAsync(request, ct);

        var grouped = paginated.Data
            .GroupBy(p => p.FuelType)
            .Select(g => new FuelHistoryResponse(
                FuelType:    FuelTypeLabels.GetValueOrDefault(g.Key, g.Key.ToString()),
                FuelTypeKey: g.Key.ToString(),
                History: g.Select(p => new PriceHistoryPoint(
                    Price:         p.Price,
                    Currency:      p.Currency,
                    EffectiveDate: p.EffectiveDate,
                    Source:        p.Source,
                    HasConflict:   p.HasConflict,
                    ConflictNote:  p.ConflictNote,
                    CanonicalCode: p.CanonicalCode
                )).ToList()
            ))
            .ToList();

        return new PaginatedResponse<FuelHistoryResponse>(
            Success:    true,
            Data:       grouped,
            Page:       paginated.Page,
            PageSize:   paginated.PageSize,
            TotalCount: paginated.TotalCount
        );
    }

    public async Task<RefreshResultDto> RefreshPricesAsync(CancellationToken ct = default)
    {
        var sw  = Stopwatch.StartNew();
        var log = await scraperLogs.CreateAsync("Arbiter", ct);

        string?   dataSource    = null;
        int       pricesUpdated = 0;
        DateOnly? effectiveDate = null;
        int       conflictsFound = 0;

        try
        {
            logger.LogInformation("Starting fuel price refresh via SourceArbiter");
            var scraped = await arbiter.GetPricesAsync(ct);

            var livePrices = scraped.Where(p => !p.IsStale).ToList();
            if (livePrices.Count > 0)
            {
                dataSource     = livePrices.First().Source;
                effectiveDate  = livePrices.Max(p => p.EffectiveDate);
                conflictsFound = livePrices.Count(p => p.HasConflict);

                log.DataSource     = dataSource;
                log.EffectiveDate  = effectiveDate;
                log.ConflictsFound = conflictsFound;
            }

            if (scraped.Count > 0 && scraped.All(p => p.IsStale))
            {
                logger.LogWarning("Refresh skipped — arbiter served LastKnownGood; no live scrape succeeded");
                log.Status  = ScraperStatus.Failed;
                log.Message = "All scrapers failed — serving LastKnownGood";
                return new RefreshResultDto(false, null, 0, null, sw.ElapsedMilliseconds, 0);
            }

            var newPrices = new List<FuelPrice>();
            foreach (var price in scraped)
            {
                var alreadyExists = price.ContentHash is not null
                    ? await repository.ExistsByContentHashAsync(price.ContentHash, ct)
                    : await repository.PriceExistsForDateAsync(price.FuelType, price.EffectiveDate, ct);

                if (alreadyExists)
                {
                    logger.LogDebug("Skipping duplicate: {FuelType} {Date} (hash={Hash})",
                        price.FuelType, price.EffectiveDate, price.ContentHash);
                    continue;
                }

                await repository.DeactivatePreviousAsync(price.FuelType, ct);
                newPrices.Add(price);
            }

            if (newPrices.Count > 0)
            {
                await repository.AddRangeAsync(newPrices, ct);
                cache.Remove(LatestCacheKey);
                cache.Remove(LatestAllCacheKey);
                logger.LogInformation("Saved {Count} new fuel prices", newPrices.Count);
            }

            pricesUpdated     = newPrices.Count;
            log.Status        = pricesUpdated > 0 ? ScraperStatus.Success : ScraperStatus.NoChanges;
            log.PricesUpdated = pricesUpdated;
            log.Message       = pricesUpdated > 0
                ? $"Updated {pricesUpdated} prices"
                : "No new prices found";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fuel price refresh failed");
            log.Status       = ScraperStatus.Failed;
            log.ErrorDetails = ex.Message;
        }
        finally
        {
            sw.Stop();
            log.CompletedAt = DateTime.UtcNow;
            await scraperLogs.UpdateAsync(log, ct);
        }

        return new RefreshResultDto(
            Updated:        log.Status == ScraperStatus.Success,
            DataSource:     dataSource,
            PricesUpdated:  pricesUpdated,
            EffectiveDate:  effectiveDate,
            DurationMs:     sw.ElapsedMilliseconds,
            ConflictsFound: conflictsFound
        );
    }

    private FuelLatestResponse MapToLatestResponse(
        IReadOnlyList<FuelPrice> prices, bool fromCache, bool includeAll = false)
    {
        var filter     = includeAll ? null : (HashSet<FuelType>?)PublicFuelTypes;
        var dataSource = prices.FirstOrDefault()?.Source;

        var dtos = prices
            .Where(p => filter is null || filter.Contains(p.FuelType))
            .OrderBy(p => p.FuelType)
            .Select(MapPrice)
            .ToList();

        return new FuelLatestResponse(dtos, DateTime.UtcNow, fromCache, Disclaimer, dataSource);
    }

    private FuelPriceDto MapPrice(FuelPrice p)
    {
        var today         = DateOnly.FromDateTime(DateTime.UtcNow);
        var consumerStale = options.Value.ConsumerStaleDays;

        return new FuelPriceDto(
            FuelType:          FuelTypeLabels.GetValueOrDefault(p.FuelType, p.FuelType.ToString()),
            FuelTypeKey:       p.FuelType.ToString(),
            Price:             p.Price,
            Currency:          p.Currency,
            EffectiveDate:     p.EffectiveDate,
            Source:            p.Source,
            SourceUrl:         p.SourceUrl,
            LastUpdated:       p.CreatedAt,
            ConfidenceScore:   p.ConfidenceScore,
            IsStale:           p.IsStale || (today.DayNumber - p.EffectiveDate.DayNumber) > consumerStale,
            SourcePriority:    p.SourcePriority,
            CanonicalCode:     p.CanonicalCode,
            PriceWithoutTax:   p.PriceWithoutTax,
            Tax:               p.Tax,
            AverageMargin:     p.AverageMargin,
            SourceUpdatedAt:   p.SourceUpdatedAt,
            FetchedAt:         p.FetchedAt == default ? null : p.FetchedAt,
            SourceProductName: p.SourceProductName,
            SourceProductId:   p.SourceProductId,
            HasConflict:       p.HasConflict,
            ConflictNote:      p.ConflictNote
        );
    }
}
