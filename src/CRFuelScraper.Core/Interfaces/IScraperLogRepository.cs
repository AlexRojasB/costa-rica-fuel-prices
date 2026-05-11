using CRFuelScraper.Core.Entities;

namespace CRFuelScraper.Core.Interfaces;

public interface IScraperLogRepository
{
    Task<ScraperLog> CreateAsync(string source, CancellationToken ct = default);
    Task UpdateAsync(ScraperLog log, CancellationToken ct = default);
    Task<IReadOnlyList<ScraperLog>> GetRecentAsync(int count = 10, CancellationToken ct = default);
    Task<int> PurgeOldLogsAsync(int retentionDays = 90, CancellationToken ct = default);
}
