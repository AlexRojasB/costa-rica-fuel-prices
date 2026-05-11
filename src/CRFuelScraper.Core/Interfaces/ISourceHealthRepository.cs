using CRFuelScraper.Core.Entities;

namespace CRFuelScraper.Core.Interfaces;

public interface ISourceHealthRepository
{
    Task RecordAsync(SourceHealth health, CancellationToken ct = default);
    Task<IReadOnlyList<SourceHealth>> GetLatestPerSourceAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SourceHealth>> GetLastSuccessPerSourceAsync(CancellationToken ct = default);
}
