namespace CRFuelScraper.Core.Options;

/// <summary>
/// Configurable thresholds for the SourceArbiter and FuelPriceService.
/// Bind from "Scraping" section in appsettings.json.
/// </summary>
public record ScrapingOptions
{
    /// <summary>A scraped effective date more than this many days behind the last known DB date is considered stale. Default: 3.</summary>
    public int StalenessThresholdDays { get; init; } = 3;

    /// <summary>If a source's published date is older than this many days the arbiter considers it stale. Default: 7.</summary>
    public int MaxPublishedAgeDays { get; init; } = 7;

    /// <summary>Minimum number of fuel prices a scraper must return to be considered valid. Default: 3.</summary>
    public int MinExpectedProducts { get; init; } = 3;

    /// <summary>Age threshold in days for marking a price stale in GET /latest responses. Default: 45.</summary>
    public int ConsumerStaleDays { get; init; } = 45;
}
