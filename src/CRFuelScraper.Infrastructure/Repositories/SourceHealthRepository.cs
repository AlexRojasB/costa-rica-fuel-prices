using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CRFuelScraper.Infrastructure.Repositories;

public class SourceHealthRepository(AppDbContext db) : ISourceHealthRepository
{
    public async Task RecordAsync(SourceHealth health, CancellationToken ct = default)
    {
        var lastRecord = await db.SourceHealthItems.AsNoTracking()
            .Where(h => h.SourceName == health.SourceName)
            .OrderByDescending(h => h.CheckedAt)
            .FirstOrDefaultAsync(ct);

        health.ConsecutiveFailures = health.Status == SourceStatus.Healthy
            ? 0
            : (lastRecord?.ConsecutiveFailures ?? 0) + 1;

        var recentMs = await db.SourceHealthItems.AsNoTracking()
            .Where(h => h.SourceName == health.SourceName && h.ResponseMs.HasValue)
            .OrderByDescending(h => h.CheckedAt)
            .Take(9)
            .Select(h => h.ResponseMs!.Value)
            .ToListAsync(ct);

        if (health.ResponseMs.HasValue)
            recentMs.Add(health.ResponseMs.Value);

        health.AvgResponseMs = recentMs.Count > 0 ? (int)recentMs.Average() : null;

        db.SourceHealthItems.Add(health);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SourceHealth>> GetLatestPerSourceAsync(CancellationToken ct = default)
    {
        var latestIds = await db.SourceHealthItems.AsNoTracking()
            .GroupBy(h => h.SourceName)
            .Select(g => g.OrderByDescending(h => h.CheckedAt).First().Id)
            .ToListAsync(ct);

        return await db.SourceHealthItems.AsNoTracking()
            .Where(h => latestIds.Contains(h.Id))
            .OrderBy(h => h.SourceName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SourceHealth>> GetLastSuccessPerSourceAsync(CancellationToken ct = default)
    {
        var successIds = await db.SourceHealthItems.AsNoTracking()
            .Where(h => h.Status == SourceStatus.Healthy)
            .GroupBy(h => h.SourceName)
            .Select(g => g.OrderByDescending(h => h.CheckedAt).First().Id)
            .ToListAsync(ct);

        return await db.SourceHealthItems.AsNoTracking()
            .Where(h => successIds.Contains(h.Id))
            .ToListAsync(ct);
    }
}
