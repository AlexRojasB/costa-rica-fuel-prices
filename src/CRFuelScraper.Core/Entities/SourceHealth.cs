using CRFuelScraper.Core.Enums;

namespace CRFuelScraper.Core.Entities;

/// <summary>
/// Records the outcome of one health check against a single data source.
/// One row is written per source per arbiter run.
/// </summary>
public class SourceHealth
{
    public int Id { get; set; }

    /// <summary>Human-readable source name, e.g. "RECOPE HTML", "RECOPE API", "ARESEP".</summary>
    public string SourceName { get; set; } = string.Empty;

    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public SourceStatus Status { get; set; }

    /// <summary>Round-trip time to fetch and parse the source, in milliseconds.</summary>
    public int? ResponseMs { get; set; }

    /// <summary>The effective date reported by the source, if extraction succeeded.</summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>Error message or staleness reason when Status != Healthy.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of consecutive non-Healthy checks for this source.
    /// Resets to 0 on the first Healthy check.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Rolling average of ResponseMs across the last 10 checks for this source.
    /// </summary>
    public int? AvgResponseMs { get; set; }
}
