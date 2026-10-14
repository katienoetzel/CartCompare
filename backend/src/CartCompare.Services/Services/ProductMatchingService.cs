using System.Text.RegularExpressions;
using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Providers.Models;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class ProductMatchingService
    : IProductMatchingService
{
    private readonly IRetailerProductRepository
        _retailerProductRepository;

    public ProductMatchingService(
        IRetailerProductRepository retailerProductRepository)
    {
        _retailerProductRepository =
            retailerProductRepository;
    }

    public async Task<ProductMatchResult> EvaluateAsync(
        Item item,
        ProviderProduct providerProduct)
    {
        var existingProducts =
            await _retailerProductRepository
                .GetByItemIdAsync(item.Id);

        // Strongest possible deterministic evidence:
        // an existing product for this canonical Item
        // already has the same UPC.
        var providerUpc =
            NormalizeUpc(providerProduct.Upc);

        if (!string.IsNullOrWhiteSpace(providerUpc))
        {
            var upcMatch =
                existingProducts.Any(product =>
                    NormalizeUpc(product.Upc) ==
                    providerUpc
                );

            if (upcMatch)
            {
                return new ProductMatchResult
                {
                    IsMatch = true,

                    MatchMethod =
                        ProductMatchMethod.Upc,

                    MatchConfidence = 1.00m,

                    Reason =
                        "UPC matches an existing retailer product for this item."
                };
            }
        }

        var itemName =
            CanonicalizeName(
                NormalizeText(item.Name)
            );

        var providerName =
            CanonicalizeName(
                NormalizeText(providerProduct.Name)
            );

        if (
            string.IsNullOrWhiteSpace(itemName) ||
            string.IsNullOrWhiteSpace(providerName))
        {
            return NoMatch(
                "A usable product name is missing."
            );
        }

        // If the canonical Item specifies a brand,
        // the provider product must agree with it.
        if (!string.IsNullOrWhiteSpace(item.Brand))
        {
            var itemBrand =
                NormalizeText(item.Brand);

            var providerBrand =
                NormalizeText(providerProduct.Brand);

            if (
                string.IsNullOrWhiteSpace(providerBrand) ||
                itemBrand != providerBrand)
            {
                return NoMatch(
                    "The product brands do not match."
                );
            }
        }

        // Same idea for size.
        // If our canonical Item says "1 gallon",
        // we should not automatically accept "half gallon".
        if (!string.IsNullOrWhiteSpace(item.Size))
        {
            if (!ProductSize.Matches(item.Size, providerProduct.Size))
            {
                return NoMatch(
                    "The product sizes do not match."
                );
            }
        }

        // Best text case:
        // normalized names are identical.
        if (itemName == providerName)
        {
            return new ProductMatchResult
            {
                IsMatch = true,

                MatchMethod =
                    ProductMatchMethod.Deterministic,

                MatchConfidence = 0.98m,

                Reason =
                    "Normalized product names match exactly."
            };
        }

        var itemTokens =
            GetNameTokens(itemName);

        var providerTokens =
            GetNameTokens(providerName);

        // We deliberately require at least two meaningful
        // tokens for a containment match.
        //
        // "Whole Milk" -> "Kroger Whole Milk" can match.
        //
        // But merely "Milk" is too vague to automatically
        // identify one specific product.
        if (
            itemTokens.Count >= 2 &&
            itemTokens.All(providerTokens.Contains))
        {
            // A product that is a different variant of the
            // same food is not an equivalent substitute.
            //
            // "Whole Milk" must not match "Chocolate Whole Milk",
            // "Lactose Free Whole Milk", or "2% Reduced Fat Milk",
            // even though the sizes agree.
            var variantReason =
                GetVariantMismatchReason(
                    item.Name,
                    itemName,
                    providerProduct.Name,
                    providerName
                );

            if (variantReason is not null)
            {
                return NoMatch(variantReason);
            }

            return new ProductMatchResult
            {
                IsMatch = true,

                MatchMethod =
                    ProductMatchMethod.Deterministic,

                MatchConfidence = 0.90m,

                Reason =
                    "All significant item-name terms appear in the provider product name."
            };
        }

        // One word ("Bananas") is too vague to find anywhere in a name:
        // "Banana Squash" is not bananas. It must be the name's last
        // word, the food the name is about, before any "(", "–" or ",".
        // Only produce sold by the pound qualifies; plain "Milk" stays
        // too vague to pick a fat level automatically.
        if (
            itemTokens.Count == 1 &&
            ProductSize.IsPerPound(item.Size) &&
            GetHeadToken(providerProduct.Name) == itemTokens.Single())
        {
            var variantReason =
                GetVariantMismatchReason(
                    item.Name,
                    itemName,
                    providerProduct.Name,
                    providerName
                );

            if (variantReason is not null)
            {
                return NoMatch(variantReason);
            }

            return new ProductMatchResult
            {
                IsMatch = true,

                MatchMethod =
                    ProductMatchMethod.Deterministic,

                MatchConfidence = 0.90m,

                Reason =
                    "The provider product name ends with the item's name."
            };
        }

        return NoMatch(
            "There is not enough deterministic evidence to match these products safely."
        );
    }

    private static string? GetHeadToken(
        string providerName)
    {
        var mainName =
            Regex.Split(providerName, @"\(|\s[–—-]\s|,")[0];

        return GetNameTokenList(
                NormalizeText(mainName)
            )
            .LastOrDefault();
    }

    private static ProductMatchResult NoMatch(
        string reason)
    {
        return new ProductMatchResult
        {
            IsMatch = false,
            MatchMethod = null,
            MatchConfidence = null,
            Reason = reason
        };
    }

    // Words that turn a product into a different variant.
    // A provider product containing one of these is only
    // accepted when the canonical item name contains it too.
    private static readonly HashSet<string> VariantTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "chocolate",
            "strawberry",
            "vanilla",
            "flavored",
            "lactose",
            "reduced",
            "fat",
            "nonfat",
            "lowfat",
            "skim",
            "organic",
            "ultra",
            "a2",
            "goat",
            "almond",
            "oat",
            "soy",
            "coconut",
            "decaf",
            "decaffeinated",
            "instant",
            "whipped",
            "spreadable",
            "gluten",
            "keto",
            "wheat",
            "grain",
            "frozen",
            "dried",
            "cooked",
            "breaded",
            "seasoned",
            "marinated",
            "spicy",
            // Egg grades: "Large Eggs" must not match extra large or jumbo.
            "extra",
            "jumbo"
        };

    // Matches fat percentages such as "2%", "1 %", or "0.5%".
    private static readonly Regex PercentPattern =
        new(@"(?<value>\d+(\.\d+)?)\s*%", RegexOptions.Compiled);

    private static string? GetVariantMismatchReason(
        string rawItemName,
        string itemName,
        string rawProviderName,
        string providerName)
    {
        // Percentages must be read from the raw names,
        // because normalization removes the "%" sign and
        // would confuse "2%" with a size such as "2 lb".
        if (
            !GetPercentages(rawProviderName)
                .SetEquals(GetPercentages(rawItemName)))
        {
            return "The provider product is a different variant (fat percentage).";
        }

        var itemTokens =
            GetNameTokens(itemName);

        var extraVariant =
            GetNameTokens(providerName)
                .Where(token =>
                    VariantTokens.Contains(token) &&
                    !itemTokens.Contains(token)
                )
                .OrderBy(token => token)
                .FirstOrDefault();

        if (extraVariant is not null)
        {
            return $"The provider product is a different variant ({extraVariant}).";
        }

        return null;
    }

    private static HashSet<decimal> GetPercentages(
        string? rawName)
    {
        return PercentPattern
            .Matches(rawName ?? string.Empty)
            .Select(match =>
                decimal.Parse(
                    match.Groups["value"].Value,
                    System.Globalization.CultureInfo.InvariantCulture
                )
            )
            .ToHashSet();
    }

    // Rewrites equivalent product descriptions into one form.
    //
    // In the United States, plain "Vitamin D Milk" is whole
    // milk; reduced-fat milks always state their fat level.
    // Harris Teeter returns its whole milk this way.
    private static string CanonicalizeName(
        string normalizedName)
    {
        return Regex.Replace(
            normalizedName,
            @"\bvitamin d milk\b",
            "vitamin d whole milk"
        );
    }

    private static string NormalizeText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized =
            value
                .Trim()
                .ToLowerInvariant();

        normalized =
            Regex.Replace(
                normalized,
                @"[^a-z0-9]+",
                " "
            );

        return Regex.Replace(
            normalized,
            @"\s+",
            " "
        ).Trim();
    }

    private static HashSet<string> GetNameTokens(
        string normalizedName)
    {
        return GetNameTokenList(normalizedName)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase
            );
    }

    private static List<string> GetNameTokenList(
        string normalizedName)
    {
        return normalizedName
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries
            )
            .Where(token =>
                token.Length > 1 ||
                token.Any(char.IsDigit)
            )
            .Select(Singular)
            .ToList();
    }

    // "Chicken Breasts" and "Chicken Breast" name the same food.
    // Words ending in "ss" (boneless, skinless) are not plurals.
    private static string Singular(
        string token)
    {
        if (token.Length <= 3 || token.Any(char.IsDigit))
        {
            return token;
        }

        if (token.EndsWith("ies"))
        {
            return token[..^3] + "y";
        }

        return token.EndsWith('s') && !token.EndsWith("ss")
            ? token[..^1]
            : token;
    }

    private static string NormalizeUpc(
        string? upc)
    {
        if (string.IsNullOrWhiteSpace(upc))
        {
            return string.Empty;
        }

        return new string(
            upc
                .Where(char.IsDigit)
                .ToArray()
        );
    }
}