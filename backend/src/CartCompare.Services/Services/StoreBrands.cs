using System.Text.RegularExpressions;

namespace CartCompare.Services.Services;

// Retailers' own labels. A store-brand product exists at one retailer
// only, so items made from one are generic: CartCompare is about
// comparing across stores.
public static class StoreBrands
{
    private static readonly HashSet<string> Labels =
        new[]
        {
            // Kroger
            "Kroger",
            "Simple Truth",
            "Simple Truth Organic",
            "Private Selection",
            "Heritage Farm",
            "Home Chef",

            // Harris Teeter
            "Harris Teeter",
            "Harris Teeter Farmers Market",
            "Harris Teeter Organics",
            "Highland Crest",

            // Lowe's Foods
            "Lowes Foods",
            "Lowe's Foods",
            "Food Club",
            "Full Circle",
            "Full Circle Market"
        }
        .Select(Normalize)
        .ToHashSet();

    public static bool IsStoreBrand(
        string? brand)
    {
        return !string.IsNullOrWhiteSpace(brand) &&
            Labels.Contains(Normalize(brand));
    }

    private static string Normalize(
        string value)
    {
        return Regex.Replace(
                Regex.Replace(value.ToLowerInvariant(), @"[™®©']", ""),
                @"[^a-z0-9]+",
                " ")
            .Trim();
    }
}
