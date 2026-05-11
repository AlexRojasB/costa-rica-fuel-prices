using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CRFuelScraper.Infrastructure.Repositories;

public class ScraperLogRepository(AppDbContext db) : IScraperLogRepository
{
    public async Task<ScraperLog> CreateAsync(string source, CancellationToken ct = default)
    {
        var log = new ScraperLog { Source = source, StartedAt = DateTime.UtcNow };
        db.ScraperLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return log;
    }

    public async Task UpdateAsync(ScraperLog log, CancellationToken ct = default)
    {
        db.ScraperLogs.Update(log);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ScraperLog>> GetRecentAsync(int count = 10, CancellationToken ct = default)
    {
        return await db.ScraperLogs
            .OrderByDescending(l => l.StartedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<int> PurgeOldLogsAsync(int retentionDays = 90, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        return await db.ScraperLogs
            .Where(l => l.StartedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
