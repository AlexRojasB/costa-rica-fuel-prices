using CRFuelScraper.Core.Entities;

namespace CRFuelScraper.Core.Interfaces;

/// <summary>
/// Orchestrates the source priority chain: tries live scrapers in order, falls back
/// to LastKnownGood when all scrapers fail.
/// </summary>
public interface ISourceArbiter
{
    Task<IReadOnlyList<FuelPrice>> GetPricesAsync(CancellationToken ct = default);
}
