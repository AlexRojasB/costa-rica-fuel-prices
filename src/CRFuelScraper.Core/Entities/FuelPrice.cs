using CRFuelScraper.Core.Enums;

namespace CRFuelScraper.Core.Entities;

public class FuelPrice
{
    public int Id { get; set; }
    public FuelType FuelType { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "CRC";
    public DateOnly EffectiveDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    /// <summary>How reliable this reading is (0.0–1.0). RECOPE API = 0.99, HTML = 0.92, ARESEP = 0.85.</summary>
    public decimal ConfidenceScore { get; set; } = 1.0m;
    /// <summary>True when this record was stored as a last-known-good fallback.</summary>
    public bool IsStale { get; set; } = false;
    /// <summary>Priority of the source that provided this price (1 = highest).</summary>
    public int SourcePriority { get; set; } = 1;
    /// <summary>SHA-256 of (CanonicalCode|Price:F4|EffectiveDate|Source). Unique per source reading.</summary>
    public string? ContentHash { get; set; }

    /// <summary>Stable short code for public API responses (e.g. "SUPER", "REGULAR", "DIESEL").</summary>
    public string CanonicalCode { get; set; } = string.Empty;
    /// <summary>Source-system product identifier (e.g. RECOPE API "id" field "000000000000080018").</summary>
    public string? SourceProductId { get; set; }
    /// <summary>Raw product name as returned by the source (e.g. "GASOLINA SUPER ( SUPERIOR )").</summary>
    public string? SourceProductName { get; set; }
    /// <summary>When the source last updated this price record (from RECOPE API "fechaupd").</summary>
    public DateOnly? SourceUpdatedAt { get; set; }
    /// <summary>UTC timestamp of when the scrape request was made.</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Price before taxes in CRC (RECOPE API "precsinimp"). Null when source doesn't publish it.</summary>
    public decimal? PriceWithoutTax { get; set; }
    /// <summary>Tax component of the total price in CRC (RECOPE API "impuesto"). Null when unavailable.</summary>
    public decimal? Tax { get; set; }
    /// <summary>Average distribution margin in CRC (RECOPE API "margenpromedio"). Null when unavailable.</summary>
    public decimal? AverageMargin { get; set; }
    /// <summary>True when two live sources report different prices for the same canonical code and date.</summary>
    public bool HasConflict { get; set; } = false;
    /// <summary>Human-readable description of the conflict when HasConflict is true.</summary>
    public string? ConflictNote { get; set; }
}
