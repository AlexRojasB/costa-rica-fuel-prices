using System.Globalization;
using System.Text.Json;
using CRFuelScraper.Core.DTOs.RecopeApi;
using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;
using CRFuelScraper.Core.Helpers;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Infrastructure.Helpers;
using Microsoft.Extensions.Logging;

namespace CRFuelScraper.Infrastructure.Services;

/// <summary>
/// Scraper for the RECOPE consumer-price REST API.
/// URL: https://api.recope.go.cr/ventas/precio/consumidor
/// Priority 1 (confidence 0.99) — preferred source.
/// </summary>
public class RecopeApiScraperService(
    IHttpClientFactory httpClientFactory,
    ILogger<RecopeApiScraperService> logger) : IFuelScraperService
{
    public string SourceName => "RECOPE API";
    public string SourceUrl  => "https://api.recope.go.cr/ventas/precio/consumidor";

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public virtual async Task<IReadOnlyList<FuelPrice>> ScrapeAsync(CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("FuelScraperJson");
        logger.LogInformation("Starting RECOPE API scrape from {Url}", SourceUrl);

        string json;
        try
        {
            using var response = await client.GetAsync(SourceUrl, ct);

            logger.LogInformation("RECOPE API responded {StatusCode} {Reason}",
                (int)response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("RECOPE API non-success {StatusCode}: body={Body}",
                    (int)response.StatusCode,
                    body.Length > 200 ? body[..200] : body);

                throw new HttpRequestException(
                    $"RECOPE API returned {(int)response.StatusCode} {response.ReasonPhrase}",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            json = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to fetch RECOPE API — status={Status}",
                ex.StatusCode.HasValue ? (int)ex.StatusCode : -1);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch RECOPE API (network/timeout)");
            throw;
        }

        var prices = ParseResponse(json);

        if (prices.Count == 0)
            throw new InvalidOperationException(
                "RECOPE API returned no recognisable fuel prices — schema may have changed");

        logger.LogInformation("RECOPE API scrape complete — {Count} prices extracted", prices.Count);
        return prices.AsReadOnly();
    }

    private List<FuelPrice> ParseResponse(string json)
    {
        var fetchedAt = DateTime.UtcNow;
        var today     = DateOnly.FromDateTime(fetchedAt);
        var results   = new List<FuelPrice>();
        var matched   = new HashSet<FuelType>();

        List<RecopeApiPriceItemDto>? items = null;
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                items = JsonSerializer.Deserialize<List<RecopeApiPriceItemDto>>(root.GetRawText(), JsonOptions);
            }
            else
            {
                foreach (var wrapper in new[] { "items", "precios", "data" })
                {
                    if (root.TryGetProperty(wrapper, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        items = JsonSerializer.Deserialize<List<RecopeApiPriceItemDto>>(arr.GetRawText(), JsonOptions);
                        break;
                    }
                }
            }
        }

        if (items is null) return results;

        foreach (var item in items)
        {
            var normalized = ProductNormalizer.Normalize(item.ProductName);
            if (normalized is null) continue;
            if (normalized.FuelType == FuelType.Kerosene) continue;
            if (matched.Contains(normalized.FuelType)) continue;

            var priceRaw = GetElementAsString(item.PrecioTotal);
            if (priceRaw is null || !PriceParser.TryParse(priceRaw, out var price)) continue;

            var effectiveDate   = TryParseDate(item.Fecha) ?? today;
            var sourceUpdatedAt = TryParseDate(item.FechaUpd);

            decimal? priceWithoutTax = TryParseDecimal(item.PrecSinImp);
            decimal? tax             = TryParseDecimal(item.Impuesto);
            decimal? averageMargin   = TryParseDecimal(item.MargenPromedio);

            results.Add(new FuelPrice
            {
                FuelType          = normalized.FuelType,
                CanonicalCode     = normalized.CanonicalCode,
                Price             = price,
                PriceWithoutTax   = priceWithoutTax,
                Tax               = tax,
                AverageMargin     = averageMargin,
                Currency          = "CRC",
                EffectiveDate     = effectiveDate,
                SourceUpdatedAt   = sourceUpdatedAt,
                SourceProductId   = item.Id,
                SourceProductName = item.NomProd,
                FetchedAt         = fetchedAt,
                Source            = SourceName,
                SourceUrl         = SourceUrl,
                CreatedAt         = fetchedAt,
                IsActive          = true
            });
            matched.Add(normalized.FuelType);
        }

        return results;
    }

    private static DateOnly? TryParseDate(string? raw)
    {
        if (raw is null) return null;

        if (raw.Length == 8 && raw.All(char.IsDigit) &&
            DateOnly.TryParseExact(raw, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
            return d1;

        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
            return d2;

        return null;
    }

    private static string? GetElementAsString(JsonElement? element)
        => element?.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number => element.Value.GetRawText(),
            _                    => null
        };

    private static decimal? TryParseDecimal(string? raw)
        => raw is not null && decimal.TryParse(raw,
               NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value : null;
}
