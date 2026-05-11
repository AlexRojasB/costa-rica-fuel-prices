using CRFuelScraper.Core.Enums;

namespace CRFuelScraper.Core.Helpers;

/// <summary>
/// Maps raw source product names to canonical fuel types and short codes.
/// </summary>
public static class ProductNormalizer
{
    public record NormalizedProduct(FuelType FuelType, string CanonicalCode);

    private static readonly (string[] Keywords, FuelType FuelType, string CanonicalCode)[] Rules =
    [
        (["gasolina super", "superior", "súper", "super"],    FuelType.Super,    "SUPER"),
        (["gasolina plus", "plus 91", "plus91", "regular"],   FuelType.Regular,  "REGULAR"),
        (["diesel 50", "diésel", "diesel", "gasoil"],         FuelType.Diesel,   "DIESEL"),
        (["kerosene", "kerosén", "keroseno"],                 FuelType.Kerosene, "KEROSENE"),
    ];

    public static NormalizedProduct? Normalize(string? rawProductName)
    {
        if (rawProductName is null) return null;

        var lower = rawProductName.ToLowerInvariant();
        foreach (var (keywords, fuelType, code) in Rules)
            if (keywords.Any(k => lower.Contains(k)))
                return new NormalizedProduct(fuelType, code);

        return null;
    }
}
