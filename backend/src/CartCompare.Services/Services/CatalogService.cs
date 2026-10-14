using System.Text.RegularExpressions;
using CartCompare.Entities;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class CatalogService : ICatalogService
{
    private readonly IItemRepository _items;
    private readonly IStoreLocationRepository _stores;
    private readonly IRetailerRepository _retailers;
    private readonly IPriceProviderResolver _providers;
    private readonly IProductMatchingService _matching;

    public CatalogService(
        IItemRepository items, IStoreLocationRepository stores,
        IRetailerRepository retailers, IPriceProviderResolver providers,
        IProductMatchingService matching)
    {
        _items = items;
        _stores = stores;
        _retailers = retailers;
        _providers = providers;
        _matching = matching;
    }

    public async Task<CatalogSearchResult> SearchAsync(
        int storeLocationId, string query)
    {
        var (result, store, retailer, provider) = await ResolveAsync(storeLocationId);
        if (result != CatalogResultType.Succeeded)
            return new CatalogSearchResult { Result = result, RetailerName = retailer?.Name };

        var products = await provider!.SearchProductsAsync(
            retailer!.Name, store!.ExternalLocationId, query.Trim());

        return new CatalogSearchResult
        {
            Result = CatalogResultType.Succeeded,
            RetailerName = retailer.Name,
            Products = products
                // Without a size an item cannot be matched automatically.
                .Where(product => !string.IsNullOrWhiteSpace(product.Size))
                .Select(product => new CatalogProduct
                {
                    ExternalProductId = product.ExternalProductId,
                    Name = product.Name,
                    Brand = product.Brand,
                    Size = product.Size,
                    ShelfPrice = product.ShelfPrice,
                    SuggestedName = SuggestName(product, retailer.Name)
                })
                .ToList()
        };
    }

    public async Task<CatalogItemResult> CreateItemFromProductAsync(
        int storeLocationId, string externalProductId,
        string name, string size, string? category)
    {
        var (result, store, retailer, provider) = await ResolveAsync(storeLocationId);
        if (result != CatalogResultType.Succeeded)
            return new CatalogItemResult { Result = result };

        // Re-read the product from the retailer rather than trusting the client.
        var product = await provider!.GetProductAsync(
            retailer!.Name, store!.ExternalLocationId, externalProductId.Trim());
        if (product is null)
            return new CatalogItemResult { Result = CatalogResultType.ProductNotFound };

        var draft = new Item
        {
            Name = Regex.Replace(name.Trim(), @"\s+", " "),
            Size = size.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            IsActive = true
        };

        // The same test comparisons use: the new item must match the picked product.
        var match = await _matching.EvaluateAsync(draft, product);
        var candidate = new ProductCandidate
        {
            ExternalProductId = product.ExternalProductId,
            Name = product.Name,
            Brand = product.Brand,
            Size = product.Size,
            IsMatch = match.IsMatch,
            MatchMethod = match.MatchMethod,
            MatchConfidence = match.MatchConfidence,
            MatchReason = match.Reason
        };
        if (!AutomaticProductMatchPolicy.IsSafe(draft, candidate))
            return new CatalogItemResult { Result = CatalogResultType.NameDoesNotMatchProduct };

        var existing = (await _items.GetActiveAsync()).FirstOrDefault(item =>
            string.Equals(item.Name.Trim(), draft.Name, StringComparison.OrdinalIgnoreCase) &&
            item.Size is not null &&
            ProductSize.Matches(item.Size, draft.Size) &&
            ProductSize.Matches(draft.Size, item.Size));
        if (existing is not null)
            return new CatalogItemResult { Result = CatalogResultType.Succeeded, Item = existing };

        draft.CreatedAt = DateTime.UtcNow;
        draft.UpdatedAt = DateTime.UtcNow;
        await _items.AddAsync(draft);

        return new CatalogItemResult { Result = CatalogResultType.Succeeded, Item = draft, Created = true };
    }

    private async Task<(CatalogResultType, StoreLocation?, Retailer?, IPriceProvider?)> ResolveAsync(
        int storeLocationId)
    {
        var store = await _stores.GetByIdAsync(storeLocationId);
        if (store is null) return (CatalogResultType.StoreNotFound, null, null, null);

        var retailer = await _retailers.GetByIdAsync(store.RetailerId);
        if (retailer is null) return (CatalogResultType.StoreNotFound, store, null, null);

        var provider = _providers.Resolve(retailer.Name);
        return provider is null
            ? (CatalogResultType.ProviderNotFound, store, retailer, null)
            : (CatalogResultType.Succeeded, store, retailer, provider);
    }

    // "Harris Teeter™ Creamy Peanut Butter 16 oz" -> "Creamy Peanut Butter".
    // Lowe's Foods reports no brands, so a leading store name counts as one.
    public static string SuggestName(ProviderProduct product, string retailerName)
    {
        var name = Regex.Replace(product.Name, @"[™®©]", "");
        name = Regex.Replace(name, @"\([^)]*\)", " ");

        var brand = string.IsNullOrWhiteSpace(product.Brand) ? retailerName : product.Brand;
        name = Regex.Replace(
            name, @"^\s*" + Regex.Escape(brand.Trim()) + @"\b", "",
            RegexOptions.IgnoreCase);

        name = ProductSize.RemoveSizes(name);
        name = Regex.Replace(name, @"\s+", " ");
        name = name.Trim(' ', ',', '-', '–', '—');

        return name.Length > 0 ? name : product.Name.Trim();
    }
}
