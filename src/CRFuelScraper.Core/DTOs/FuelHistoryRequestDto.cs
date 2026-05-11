using System.ComponentModel.DataAnnotations;

namespace CRFuelScraper.Core.DTOs;

public class FuelHistoryRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To   { get; set; }

    /// <summary>Filter by fuel type name (Super | Regular | Diesel | Kerosene).</summary>
    public string? FuelType { get; set; }

    /// <summary>Filter by canonical code (super | regular | diesel | kerosene). Alias for FuelType.</summary>
    public string? CanonicalCode { get; set; }

    /// <summary>Filter by data source name (e.g. "RECOPE API" or "RECOPE HTML").</summary>
    public string? Source { get; set; }

    /// <summary>When true, returns only records where a cross-source conflict was detected.</summary>
    public bool? HasConflict { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 30;
}
