using CRFuelScraper.Core.DTOs;
using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CRFuelScraper.Infrastructure.Repositories;

public class FuelPriceRepository(AppDbContext db) : IFuelPriceRepository
{
    public async Task<IReadOnlyList<FuelPrice>> GetLatestPricesAsync(CancellationToken ct = default)
    {
        return await db.FuelPrices.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.FuelType)
            .ToListAsync(ct);
    }

    public async Task<FuelPrice?> GetLatestByFuelTypeAsync(FuelType fuelType, CancellationToken ct = default)
    {
        return await db.FuelPrices.AsNoTracking()
            .Where(p => p.FuelType == fuelType && p.IsActive)
            .OrderByDescending(p => p.EffectiveDate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginatedResponse<FuelPrice>> GetHistoryAsync(FuelHistoryRequest request, CancellationToken ct = default)
    {
        var query = db.FuelPrices.AsQueryable();

        if (request.From.HasValue)
            query = query.Where(p => p.EffectiveDate >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(p => p.EffectiveDate <= request.To.Value);

        var fuelTypeName  = request.FuelType;
        var canonicalCode = request.CanonicalCode;

        if (!string.IsNullOrWhiteSpace(fuelTypeName) &&
            Enum.TryParse<FuelType>(fuelTypeName, ignoreCase: true, out var ft))
            query = query.Where(p => p.FuelType == ft);
        else if (!string.IsNullOrWhiteSpace(canonicalCode))
            query = query.Where(p => p.CanonicalCode == canonicalCode.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(request.Source))
            query = query.Where(p => p.Source == request.Source);

        if (request.HasConflict.HasValue)
            query = query.Where(p => p.HasConflict == request.HasConflict.Value);

        var total = await query.CountAsync(ct);

        var data = await query.AsNoTracking()
            .OrderByDescending(p => p.EffectiveDate)
            .ThenBy(p => p.FuelType)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PaginatedResponse<FuelPrice>(
            Success: true,
            Data: data,
            Page: request.Page,
            PageSize: request.PageSize,
            TotalCount: total
        );
    }

    public async Task<DateOnly?> GetLatestEffectiveDateAsync(CancellationToken ct = default)
    {
        var any = await db.FuelPrices.AnyAsync(ct);
        if (!any) return null;
        return await db.FuelPrices.MaxAsync(p => p.EffectiveDate, ct);
    }

    public async Task<bool> PriceExistsForDateAsync(FuelType fuelType, DateOnly date, CancellationToken ct = default)
    {
        return await db.FuelPrices
            .AnyAsync(p => p.FuelType == fuelType && p.EffectiveDate == date && p.IsActive, ct);
    }

    public async Task<bool> ExistsByContentHashAsync(string contentHash, CancellationToken ct = default)
    {
        return await db.FuelPrices
            .AnyAsync(p => p.ContentHash == contentHash, ct);
    }

    public async Task AddAsync(FuelPrice price, CancellationToken ct = default)
    {
        db.FuelPrices.Add(price);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<FuelPrice> prices, CancellationToken ct = default)
    {
        db.FuelPrices.AddRange(prices);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeactivatePreviousAsync(FuelType fuelType, CancellationToken ct = default)
    {
        await db.FuelPrices
            .Where(p => p.FuelType == fuelType && p.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), ct);
    }
}
