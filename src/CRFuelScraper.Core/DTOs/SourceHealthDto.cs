namespace CRFuelScraper.Core.DTOs;

public record SourceHealthDto(
    string Name,
    int Priority,
    string Status,
    DateTime LastChecked,
    DateTime? LastSuccess,
    int? ResponseMs,
    DateOnly? LastEffectiveDate,
    string? Error,
    int ConsecutiveFailures = 0,
    int? AvgResponseMs      = null
);

public record SourcesHealthResponse(
    IReadOnlyList<SourceHealthDto> Sources,
    DateTime RetrievedAt
);

public record FuelTypeInfo(string Key, string DisplayName, string Description);

public record DataProviderInfo(string Name, string FullName, string Url, string SourceUrl);

public record RegulatoryBodyInfo(string Name, string FullName, string Url);

public record FuelMetadataResponse(
    string Country,
    string Currency,
    IReadOnlyList<FuelTypeInfo> FuelTypes,
    DataProviderInfo DataProvider,
    RegulatoryBodyInfo RegulatoryBody,
    string UpdateFrequency,
    string Timezone,
    string Disclaimer
);
