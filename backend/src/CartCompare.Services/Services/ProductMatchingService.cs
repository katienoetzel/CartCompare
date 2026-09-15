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
            NormalizeText(item.Name);

        var providerName =
            NormalizeText(providerProduct.Name);

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
            var itemSize =
                NormalizeSize(item.Size);

            var providerSize =
                NormalizeSize(providerProduct.Size);

            if (
                string.IsNullOrWhiteSpace(providerSize) ||
                itemSize != providerSize)
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

        return NoMatch(
            "There is not enough deterministic evidence to match these products safely."
        );
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
        return normalizedName
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries
            )
            .Where(token =>
                token.Length > 1 ||
                token.Any(char.IsDigit)
            )
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase
            );
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

    private static string NormalizeSize(
        string? size)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            return string.Empty;
        }

        var normalized =
            size
                .Trim()
                .ToLowerInvariant();

        normalized =
            normalized
                .Replace("gallons", "gal")
                .Replace("gallon", "gal")
                .Replace("ounces", "oz")
                .Replace("ounce", "oz")
                .Replace("pounds", "lb")
                .Replace("pound", "lb")
                .Replace("count", "ct");

        normalized =
            Regex.Replace(
                normalized,
                @"\s+",
                ""
            );

        return normalized;
    }
}