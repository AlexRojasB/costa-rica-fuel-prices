using System.Security.Cryptography;
using System.Text;

namespace CRFuelScraper.Infrastructure.Helpers;

/// <summary>
/// Produces a stable fingerprint for a fuel price reading.
/// Formula: SHA-256 of "CANONICALCODE|price:F4|yyyy-MM-dd|SOURCE"
/// </summary>
internal static class ContentHasher
{
    public static string Compute(string canonicalCode, decimal price, DateOnly effectiveDate, string source)
    {
        var input = $"{canonicalCode}|{price:F4}|{effectiveDate:yyyy-MM-dd}|{source}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
