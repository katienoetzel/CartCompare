using System.Text.RegularExpressions;
using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public static class AutomaticProductMatchPolicy
{
    public static bool IsSafe(Item item, ProductCandidate candidate)
    {
        if (!candidate.IsMatch) return false;
        if (candidate.MatchMethod == ProductMatchMethod.Upc &&
            candidate.MatchConfidence == 1m) return true;

        // Containment alone accepts variants such as chocolate whole milk.
        // Require size evidence and reject unexplained extra name terms.
        if (candidate.MatchMethod != ProductMatchMethod.Deterministic ||
            candidate.MatchConfidence is null or < 0.90m ||
            string.IsNullOrWhiteSpace(item.Size) ||
            string.IsNullOrWhiteSpace(candidate.Size)) return false;

        var itemTerms = Tokens(item.Name);
        // In the United States, Vitamin D milk is whole milk; the matcher
        // already treats them as equal, so the phrase is not a variant.
        var productTerms = Tokens(Regex.Replace(
            candidate.Name, @"\bvitamin\s+d\b", " ", RegexOptions.IgnoreCase));
        var variantTerms = new HashSet<string>
        {
            "organic", "lactose", "free", "chocolate", "strawberry", "vanilla",
            "almond", "oat", "soy", "skim", "nonfat", "reduced", "ultrafiltered", "a2"
        };
        if (productTerms.Intersect(variantTerms).Except(itemTerms).Any()) return false;
        var allowedExtras = Tokens(candidate.Brand ?? "");
        allowedExtras.UnionWith(Tokens(candidate.Size).Select(Unit));
        return itemTerms.Count > 0 && itemTerms.IsSubsetOf(productTerms) &&
            productTerms.Except(itemTerms).All(term => allowedExtras.Contains(Unit(term)));
    }

    private static HashSet<string> Tokens(string value) =>
        Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
            .Select(match => match.Value).ToHashSet();

    // "Gallon" in a name restates a "1 gal" size; it is not an extra term.
    private static string Unit(string term) => term switch
    {
        "gallon" or "gallons" => "gal",
        "ounce" or "ounces" => "oz",
        "pound" or "pounds" or "lbs" => "lb",
        "count" => "ct",
        _ => term
    };
}
