namespace CRFuelScraper.Core.DTOs;

public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message,
    IReadOnlyList<string>? Errors = null
)
{
    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new(true, data, message);

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) =>
        new(false, default, message, errors);
}

public record PaginatedResponse<T>(
    bool Success,
    IReadOnlyList<T> Data,
    int Page,
    int PageSize,
    int TotalCount,
    string? Message = null
)
{
    public int TotalPages       => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage     => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>Result of a manual or scheduled price refresh cycle.</summary>
public record RefreshResultDto(
    bool Updated,
    string? DataSource,
    int PricesUpdated,
    DateOnly? EffectiveDate,
    long DurationMs,
    int ConflictsFound
);
