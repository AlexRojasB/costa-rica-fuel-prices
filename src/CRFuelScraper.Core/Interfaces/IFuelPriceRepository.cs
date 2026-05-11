using CRFuelScraper.Core.DTOs;
using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;

namespace CRFuelScraper.Core.Interfaces;

public interface IFuelPriceRepository
{
    Task<IReadOnlyList<FuelPrice>> GetLatestPricesAsync(CancellationToken ct = default);
    Task<FuelPrice?> GetLatestByFuelTypeAsync(FuelType fuelType, CancellationToken ct = default);
    Task<DateOnly?> GetLatestEffectiveDateAsync(CancellationToken ct = default);
    Task<PaginatedResponse<FuelPrice>> GetHistoryAsync(FuelHistoryRequest request, CancellationToken ct = default);
    Task<bool> PriceExistsForDateAsync(FuelType fuelType, DateOnly date, CancellationToken ct = default);
    Task<bool> ExistsByContentHashAsync(string contentHash, CancellationToken ct = default);
    Task AddAsync(FuelPrice price, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<FuelPrice> prices, CancellationToken ct = default);
    Task DeactivatePreviousAsync(FuelType fuelType, CancellationToken ct = default);
}
