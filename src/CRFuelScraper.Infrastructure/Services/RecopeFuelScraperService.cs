using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;
using CRFuelScraper.Core.Helpers;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Infrastructure.Helpers;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CRFuelScraper.Infrastructure.Services;

/// <summary>
/// Scraper for the RECOPE "tabla de precios nacionales" HTML page.
/// Priority 2 (confidence 0.92) — fallback when the RECOPE API endpoint is unavailable.
/// </summary>
public class RecopeFuelScraperService(
    IHttpClientFactory httpClientFactory,
    ILogger<RecopeFuelScraperService> logger) : IFuelScraperService
{
    public string SourceName => "RECOPE HTML";
    public string SourceUrl  => "https://www.recope.go.cr/productos/precios-nacionales/tabla-precios/";

    public virtual async Task<IReadOnlyList<FuelPrice>> ScrapeAsync(CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("FuelScraper");
        logger.LogInformation("Starting RECOPE HTML scrape from {Url}", SourceUrl);

        string html;
        try
        {
            html = await client.GetStringAsync(SourceUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch RECOPE page");
            throw;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var prices = ExtractEstacionesDeServicio(doc);

        if (prices.Count == 0)
            throw new InvalidOperationException(
                "Could not extract fuel prices from RECOPE HTML — page structure may have changed");

        logger.LogInformation("RECOPE HTML scrape complete — {Count} prices extracted", prices.Count);
        return prices.AsReadOnly();
    }

    private const int ExpectedFuelTypesPerTable = 3;

    private List<FuelPrice> ExtractEstacionesDeServicio(HtmlDocument doc)
    {
        var fetchedAt = DateTime.UtcNow;
        var today     = DateOnly.FromDateTime(fetchedAt);
        var results   = new List<FuelPrice>();

        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables is null) return results;

        foreach (var table in tables)
        {
            var rows = table.SelectNodes(".//tr");
            if (rows is null || rows.Count < 2) continue;

            var tableResults = new List<FuelPrice>();
            var matchedTypes = new HashSet<FuelType>();

            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//td");
                if (cells is null || cells.Count < 2) continue;

                var rawName    = cells[0].InnerText.Trim();
                var normalized = ProductNormalizer.Normalize(rawName);
                if (normalized is null) continue;
                if (normalized.FuelType == FuelType.Kerosene) continue;
                if (matchedTypes.Contains(normalized.FuelType)) continue;

                var lastCell = cells[^1].InnerText.Trim();
                if (!PriceParser.TryParse(lastCell, out var price)) continue;

                tableResults.Add(new FuelPrice
                {
                    FuelType          = normalized.FuelType,
                    CanonicalCode     = normalized.CanonicalCode,
                    SourceProductName = rawName,
                    Price             = price,
                    Currency          = "CRC",
                    EffectiveDate     = today,
                    FetchedAt         = fetchedAt,
                    Source            = SourceName,
                    SourceUrl         = SourceUrl,
                    CreatedAt         = fetchedAt,
                    IsActive          = true
                });
                matchedTypes.Add(normalized.FuelType);

                logger.LogDebug("Extracted {FuelType}: {Price} from '{Product}'",
                    normalized.FuelType, price, rawName);
            }

            if (tableResults.Count >= ExpectedFuelTypesPerTable)
            {
                results.AddRange(tableResults);
                break;
            }

            if (tableResults.Count > results.Count)
            {
                logger.LogWarning(
                    "Table yielded only {Found}/{Expected} fuel types — keeping as partial fallback",
                    tableResults.Count, ExpectedFuelTypesPerTable);
                results = tableResults;
            }
        }

        return results;
    }
}
