using System.Globalization;
using System.Text.RegularExpressions;

namespace CartCompare.Services.Services;

// Reads package sizes such as "1 gallon", "4 ct / 16 oz" or "per lb".
public static class ProductSize
{
    private const decimal OuncesPerPound = 16m;

    // Sizes match when they state the same amount in any unit:
    // "1 lb" = "16 oz", "1 gallon" = "128 fl oz", "12 count" = "1 dozen".
    // An item priced "per lb" matches weighed products and fixed-weight
    // packages alike; the comparison converts packages to a per-pound price.
    // A size we cannot read falls back to a plain text comparison.
    public static bool Matches(
        string itemSize,
        string? productSize)
    {
        if (string.IsNullOrWhiteSpace(productSize))
        {
            return false;
        }

        if (IsPerPound(itemSize))
        {
            return IsPerPound(productSize) || Pounds(productSize) is not null;
        }

        var itemAmounts = Parse(itemSize);
        var productAmounts = Parse(productSize);

        if (itemAmounts.Count == 0 || productAmounts.Count == 0)
        {
            return Normalize(itemSize) == Normalize(productSize);
        }

        // "4 ct / 16 oz" states two sizes; agreeing on either is enough.
        return itemAmounts.Any(item =>
            productAmounts.Any(product =>
                item.Dimension == product.Dimension &&
                Math.Abs(item.Amount - product.Amount) <= item.Amount * 0.01m
            )
        );
    }

    // "per lb": the price is per pound and the quantity is in pounds.
    public static bool IsPerPound(
        string? size)
    {
        return size is not null &&
            Regex.IsMatch(size, @"^\s*(per|/)\s*(lb|lbs|pound)\s*$", RegexOptions.IgnoreCase);
    }

    // The package weight in pounds, or null when the size states no weight.
    public static decimal? Pounds(
        string? size)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            return null;
        }

        var ounces = Parse(size)
            .Where(amount => amount.Dimension == "weight")
            .Select(amount => (decimal?)amount.Amount)
            .FirstOrDefault();

        return ounces / OuncesPerPound;
    }

    // "Corn Flakes 12 oz, 2 ct" -> "Corn Flakes ,"
    public static string RemoveSizes(
        string text)
    {
        return SizePattern.Replace(text, " ");
    }

    private static readonly Regex SizePattern =
        new(
            @"(?<amount>\d+/\d+|\d+(\.\d+)?|half)\s*-?\s*(?<unit>fl\.?\s*oz|fluid\s+ounces?|oz|ounces?|lbs?|pounds?|gal(lons?)?|qts?|quarts?|pts?|pints?|ml|l|liters?|litres?|ct|count|ea|each|pk|pack|dozen|doz)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

    // Dimension: "weight" in ounces, "volume" in fluid ounces, "count" in units.
    private static List<(string Dimension, decimal Amount)> Parse(
        string size)
    {
        var amounts = new List<(string, decimal)>();

        foreach (Match match in SizePattern.Matches(size))
        {
            var amount = ParseAmount(match.Groups["amount"].Value);
            var unit = Regex.Replace(match.Groups["unit"].Value.ToLowerInvariant(), @"[\s.]", "");

            (string Dimension, decimal Factor)? scale = unit switch
            {
                "oz" or "ounce" or "ounces" => ("weight", 1m),
                "lb" or "lbs" or "pound" or "pounds" => ("weight", OuncesPerPound),
                "floz" or "fluidounce" or "fluidounces" => ("volume", 1m),
                "gal" or "gallon" or "gallons" => ("volume", 128m),
                "qt" or "qts" or "quart" or "quarts" => ("volume", 32m),
                "pt" or "pts" or "pint" or "pints" => ("volume", 16m),
                "ml" => ("volume", 0.033814m),
                "l" or "liter" or "liters" or "litre" or "litres" => ("volume", 33.814m),
                "ct" or "count" or "ea" or "each" or "pk" or "pack" => ("count", 1m),
                "dozen" or "doz" => ("count", 12m),
                _ => null
            };

            if (amount > 0 && scale is not null)
            {
                amounts.Add((scale.Value.Dimension, amount * scale.Value.Factor));
            }
        }

        // A bare "dozen" ("Large Eggs, Dozen") has no number before it.
        if (amounts.Count == 0 && Regex.IsMatch(size, @"\bdozen\b", RegexOptions.IgnoreCase))
        {
            amounts.Add(("count", 12m));
        }

        return amounts;
    }

    private static decimal ParseAmount(
        string value)
    {
        if (value.Equals("half", StringComparison.OrdinalIgnoreCase))
        {
            return 0.5m;
        }

        var parts = value.Split('/');

        return parts.Length == 2
            ? decimal.Parse(parts[0], CultureInfo.InvariantCulture)
                / decimal.Parse(parts[1], CultureInfo.InvariantCulture)
            : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string Normalize(
        string size)
    {
        var normalized =
            size
                .Trim()
                .ToLowerInvariant()
                .Replace("gallons", "gal")
                .Replace("gallon", "gal")
                .Replace("ounces", "oz")
                .Replace("ounce", "oz")
                .Replace("pounds", "lb")
                .Replace("pound", "lb")
                .Replace("count", "ct");

        return Regex.Replace(normalized, @"\s+", "");
    }
}
