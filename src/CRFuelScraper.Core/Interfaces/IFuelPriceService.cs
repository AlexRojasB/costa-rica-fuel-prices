using CRFuelScraper.Core.DTOs;

namespace CRFuelScraper.Core.Interfaces;

public interface IFuelPriceService
{
    /// <summary>
    /// Returns the latest active fuel prices, cached for 1 hour.
    /// When <paramref name="includeAll"/> is true, includes Kerosene in addition to Super, Regular, Diesel.
    /// </summary>
    Task<FuelLatestResponse> GetLatestPricesAsync(
        bool includeAll = false, CancellationToken ct = default);

    /// <summary>Returns the latest active price for a single canonical code (e.g. "super").</summary>
    Task<FuelPriceDto?> GetPriceByCanonicalCodeAsync(
        string canonicalCode, CancellationToken ct = default);

    Task<PaginatedResponse<FuelHistoryResponse>> GetHistoryAsync(
        FuelHistoryRequest request, CancellationToken ct = default);

    Task<RefreshResultDto> RefreshPricesAsync(CancellationToken ct = default);
}
