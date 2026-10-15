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
    // Enough to show the likely candidates without a long list.
    private const int MaxExistingItems = 10;

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
                .Select(ToCatalogProduct)
                .ToList()
        };
    }

    public async Task<CatalogProductOptions> GetOptionsAsync(
        int storeLocationId, string externalProductId)
    {
        var (result, store, retailer, provider) = await ResolveAsync(storeLocationId);
        if (result != CatalogResultType.Succeeded)
            return new CatalogProductOptions { Result = result };

        // Re-read the product from the retailer rather than trusting the client.
        var product = await provider!.GetProductAsync(
            retailer!.Name, store!.ExternalLocationId, externalProductId.Trim());
        if (product is null || string.IsNullOrWhiteSpace(product.Size))
            return new CatalogProductOptions { Result = CatalogResultType.ProductNotFound };

        var catalog = await _items.GetActiveAsync();

        var existing = catalog
            .Where(item => CountsAs(item, product))
            .OrderBy(item => item.Brand is null) // Exact-brand items first.
            .ThenBy(item => item.Name)
            .Take(MaxExistingItems)
            .ToList();

        return new CatalogProductOptions
        {
            Result = CatalogResultType.Succeeded,
            Product = ToCatalogProduct(product),
            ExistingItems = existing,
            NewItems = BuildOptions(product, retailer.Name)
                .Where(option => !catalog.Any(item => IsEquivalent(item, option)))
                .ToList()
        };
    }

    public async Task<CatalogItemResult> CreateItemAsync(
        int storeLocationId, string externalProductId,
        CatalogItemKind kind, int createdByUserId)
    {
        var (result, store, retailer, provider) = await ResolveAsync(storeLocationId);
        if (result != CatalogResultType.Succeeded)
            return new CatalogItemResult { Result = result };

        var product = await provider!.GetProductAsync(
            retailer!.Name, store!.ExternalLocationId, externalProductId.Trim());
        if (product is null || string.IsNullOrWhiteSpace(product.Size))
            return new CatalogItemResult { Result = CatalogResultType.ProductNotFound };

        // The server rebuilds the option; the request only names its kind.
        var option = BuildOptions(product, retailer.Name)
            .FirstOrDefault(candidate => candidate.Kind == kind);
        if (option is null)
            return new CatalogItemResult { Result = CatalogResultType.OptionNotAvailable };

        var existing = (await _items.GetActiveAsync())
            .FirstOrDefault(item => IsEquivalent(item, option));
        if (existing is not null)
            return new CatalogItemResult { Result = CatalogResultType.Succeeded, Item = existing };

        var item = new Item
        {
            Name = option.Name,
            Brand = option.Brand,
            Size = option.Size,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _items.AddAsync(item);

        return new CatalogItemResult { Result = CatalogResultType.Succeeded, Item = item, Created = true };
    }

    // The branded and generic items this product could become. Each is
    // kept only if it would match the product it came from.
    private List<CatalogItemOption> BuildOptions(
        ProviderProduct product, string retailerName)
    {
        var brand = product.Brand?.Trim();
        var name = BaseName(product, retailerName);
        var size = product.Size!.Trim();
        var options = new List<CatalogItemOption>();

        // A store brand only exists at one retailer, so an item tied to it
        // could never be compared across stores.
        if (!string.IsNullOrWhiteSpace(brand) && !StoreBrands.IsStoreBrand(brand) &&
            !string.Equals(brand, retailerName, StringComparison.OrdinalIgnoreCase))
        {
            options.Add(new CatalogItemOption
            {
                Kind = CatalogItemKind.Branded, Name = name, Brand = brand, Size = size
            });
        }

        options.Add(new CatalogItemOption
        {
            Kind = CatalogItemKind.Generic, Name = name, Size = size
        });

        return options
            .Where(option => CountsAs(ToItem(option), product))
            .ToList();
    }

    private bool CountsAs(Item item, ProviderProduct product)
    {
        var match = _matching.EvaluateDescription(item, product);

        return AutomaticProductMatchPolicy.IsSafe(item, new ProductCandidate
        {
            ExternalProductId = product.ExternalProductId,
            Name = product.Name,
            Brand = product.Brand,
            Size = product.Size,
            IsMatch = match.IsMatch,
            MatchMethod = match.MatchMethod,
            MatchConfidence = match.MatchConfidence,
            MatchReason = match.Reason
        });
    }

    // Same name, same brand (or both generic), same size in any unit.
    private static bool IsEquivalent(Item item, CatalogItemOption option)
    {
        return string.Equals(item.Name.Trim(), option.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Brand?.Trim() ?? "", option.Brand ?? "", StringComparison.OrdinalIgnoreCase) &&
            item.Size is not null &&
            ProductSize.Matches(item.Size, option.Size) &&
            ProductSize.Matches(option.Size, item.Size);
    }

    private static Item ToItem(CatalogItemOption option) => new()
    {
        Name = option.Name, Brand = option.Brand, Size = option.Size, IsActive = true
    };

    private static CatalogProduct ToCatalogProduct(ProviderProduct product) => new()
    {
        ExternalProductId = product.ExternalProductId,
        Name = product.Name,
        Brand = product.Brand,
        Size = product.Size,
        ShelfPrice = product.ShelfPrice
    };

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

    // The product name without its brand, size and trademark text:
    // "Harris Teeter™ Creamy Peanut Butter 16 oz" -> "Creamy Peanut Butter".
    // Lowe's Foods reports no brands, so a leading store name counts as one.
    public static string BaseName(ProviderProduct product, string retailerName)
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
