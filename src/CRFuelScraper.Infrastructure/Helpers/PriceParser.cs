using System.Globalization;
using System.Text.RegularExpressions;

namespace CRFuelScraper.Infrastructure.Helpers;

/// <summary>
/// Shared helper for parsing price strings scraped from HTML tables.
/// Accepts formats like "733.0000", "733,0000", "733", "¢733".
/// </summary>
internal static partial class PriceParser
{
    [GeneratedRegex(@"[^\d.,]")]
    private static partial Regex NonNumericPattern();

    public static bool TryParse(string text, out decimal price)
    {
        price = 0;
        var clean = NonNumericPattern().Replace(text, "");

        if (clean.Contains(',') && !clean.Contains('.'))
            clean = clean.Replace(",", ".");

        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out price)
               && price > 500 && price < 10_000;
    }
}
