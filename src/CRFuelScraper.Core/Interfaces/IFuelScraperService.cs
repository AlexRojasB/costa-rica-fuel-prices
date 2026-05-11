using CRFuelScraper.Core.Entities;

namespace CRFuelScraper.Core.Interfaces;

public interface IFuelScraperService
{
    string SourceName { get; }
    string SourceUrl { get; }
    Task<IReadOnlyList<FuelPrice>> ScrapeAsync(CancellationToken ct = default);
}
