namespace CRFuelScraper.Core.Enums;

public enum SourceStatus
{
    /// <summary>Source responded correctly with fresh data.</summary>
    Healthy,

    /// <summary>Source responded but returned stale or incomplete data.</summary>
    Degraded,

    /// <summary>Source failed to respond or threw an exception.</summary>
    Down
}
