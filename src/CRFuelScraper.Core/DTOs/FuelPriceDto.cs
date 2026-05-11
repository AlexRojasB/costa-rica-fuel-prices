namespace CRFuelScraper.Core.DTOs;

public record FuelPriceDto(
    string FuelType,
    string FuelTypeKey,
    decimal Price,
    string Currency,
    DateOnly EffectiveDate,
    string Source,
    string SourceUrl,
    DateTime LastUpdated,
    decimal ConfidenceScore,
    bool IsStale,
    int SourcePriority,
    string CanonicalCode               = "",
    decimal? PriceWithoutTax           = null,
    decimal? Tax                       = null,
    decimal? AverageMargin             = null,
    DateOnly? SourceUpdatedAt          = null,
    DateTime? FetchedAt                = null,
    string? SourceProductName          = null,
    string? SourceProductId            = null,
    bool HasConflict                   = false,
    string? ConflictNote               = null
);

public record FuelLatestResponse(
    IReadOnlyList<FuelPriceDto> Prices,
    DateTime RetrievedAt,
    bool FromCache,
    string Disclaimer,
    string? DataSource = null
);

public record FuelHistoryResponse(
    string FuelType,
    string FuelTypeKey,
    IReadOnlyList<PriceHistoryPoint> History
);

public record PriceHistoryPoint(
    decimal Price,
    string Currency,
    DateOnly EffectiveDate,
    string Source,
    bool HasConflict        = false,
    string? ConflictNote    = null,
    string CanonicalCode    = ""
);
