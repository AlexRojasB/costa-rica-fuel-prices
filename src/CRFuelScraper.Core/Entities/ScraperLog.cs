namespace CRFuelScraper.Core.Entities;

public class ScraperLog
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ScraperStatus Status { get; set; } = ScraperStatus.Running;
    public string? Message { get; set; }
    public string? ErrorDetails { get; set; }
    public int PricesUpdated { get; set; } = 0;
    public string Source { get; set; } = string.Empty;

    /// <summary>Name of the source that won the arbiter race (e.g. "RECOPE API").</summary>
    public string? DataSource { get; set; }
    /// <summary>Number of fuel types where two sources reported different prices.</summary>
    public int ConflictsFound { get; set; } = 0;
    /// <summary>Effective date of the prices that were scraped in this run.</summary>
    public DateOnly? EffectiveDate { get; set; }
}

public enum ScraperStatus
{
    Running = 0,
    Success = 1,
    Failed = 2,
    NoChanges = 3
}
