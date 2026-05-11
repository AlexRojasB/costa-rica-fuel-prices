using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;
using CRFuelScraper.Core.Helpers;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Infrastructure.Helpers;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CRFuelScraper.Infrastructure.Services;

/// <summary>
/// Fallback scraper — obtiene precios desde la sección de tarifas de ARESEP.
/// Priority 3 (confidence 0.85) — used only when both RECOPE sources fail.
/// </summary>
public class AresepFuelScraperService(
    IHttpClientFactory httpClientFactory,
    ILogger<AresepFuelScraperService> logger) : IFuelScraperService
{
    public string SourceName => "ARESEP";
    public string SourceUrl  => "https://aresep.go.cr/index.php/combustibles/precios";

    public virtual async Task<IReadOnlyList<FuelPrice>> ScrapeAsync(CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("FuelScraper");
        logger.LogInformation("Starting ARESEP fallback scrape from {Url}", SourceUrl);

        string html;
        try
        {
            html = await client.GetStringAsync(SourceUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch ARESEP page");
            throw;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var prices = ExtractFromTables(doc).ToList();

        if (prices.Count == 0)
            throw new InvalidOperationException("ARESEP scraper found no prices — HTML may have changed");

        logger.LogInformation("ARESEP scrape returned {Count} prices", prices.Count);
        return prices.AsReadOnly();
    }

    private IEnumerable<FuelPrice> ExtractFromTables(HtmlDocument doc)
    {
        var fetchedAt = DateTime.UtcNow;
        var today     = DateOnly.FromDateTime(fetchedAt);
        var found     = new List<FuelPrice>();
        var matched   = new HashSet<FuelType>();

        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables is null) yield break;

        foreach (var table in tables)
        {
            var rows = table.SelectNodes(".//tr");
            if (rows is null) continue;

            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//td|.//th");
                if (cells is null || cells.Count < 2) continue;

                var texts = cells.Select(c => c.InnerText.Trim()).ToArray();

                for (int col = 0; col < texts.Length; col++)
                {
                    var normalized = ProductNormalizer.Normalize(texts[col]);
                    if (normalized is null) continue;
                    if (matched.Contains(normalized.FuelType)) continue;

                    // ARESEP table: Producto | Precio Anterior | Incremento | Precio Actual
                    // Take the last numeric value (current price, not the increment).
                    decimal lastPrice = 0;
                    for (int i = col + 1; i < texts.Length; i++)
                    {
                        if (PriceParser.TryParse(texts[i], out var p)) lastPrice = p;
                    }

                    if (lastPrice == 0) continue;

                    found.Add(new FuelPrice
                    {
                        FuelType          = normalized.FuelType,
                        CanonicalCode     = normalized.CanonicalCode,
                        SourceProductName = texts[col].Trim(),
                        Price             = lastPrice,
                        Currency          = "CRC",
                        EffectiveDate     = today,
                        FetchedAt         = fetchedAt,
                        Source            = SourceName,
                        SourceUrl         = SourceUrl,
                        CreatedAt         = fetchedAt,
                        IsActive          = true
                    });
                    matched.Add(normalized.FuelType);
                }
            }

            if (found.Count >= 3) break;
        }

        foreach (var p in found) yield return p;
    }
}
