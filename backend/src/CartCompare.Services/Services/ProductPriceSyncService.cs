using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Providers.Interfaces;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class ProductPriceSyncService
    : IProductPriceSyncService
{
    private readonly IItemRepository
        _itemRepository;

    private readonly IStoreLocationRepository
        _storeLocationRepository;

    private readonly IRetailerRepository
        _retailerRepository;

    private readonly IRetailerProductRepository
        _retailerProductRepository;

    private readonly IPriceProviderResolver
        _providerResolver;

    private readonly IPriceService
        _priceService;

    private readonly IProductMatchingService
        _productMatchingService;

    public ProductPriceSyncService(
        IItemRepository itemRepository,
        IStoreLocationRepository storeLocationRepository,
        IRetailerRepository retailerRepository,
        IRetailerProductRepository retailerProductRepository,
        IPriceProviderResolver providerResolver,
        IPriceService priceService,
        IProductMatchingService productMatchingService)
    {
        _itemRepository =
            itemRepository;

        _storeLocationRepository =
            storeLocationRepository;

        _retailerRepository =
            retailerRepository;

        _retailerProductRepository =
            retailerProductRepository;

        _providerResolver =
            providerResolver;

        _priceService =
            priceService;

        _productMatchingService =
            productMatchingService;
    }

    public async Task<ProductPriceSyncResult> SyncAsync(
        int itemId,
        int storeLocationId,
        string externalProductId)
    {
        // -------------------------------------------------
        // 1. Find the canonical CartCompare Item
        // -------------------------------------------------

        var item =
            await _itemRepository.GetByIdAsync(
                itemId
            );

        if (item is null)
        {
            return new ProductPriceSyncResult
            {
                Result =
                    ProductPriceSyncResultType
                        .ItemNotFound
            };
        }

        // -------------------------------------------------
        // 2. Find the physical store
        // -------------------------------------------------

        var store =
            await _storeLocationRepository
                .GetByIdAsync(
                    storeLocationId
                );

        if (store is null)
        {
            return new ProductPriceSyncResult
            {
                Result =
                    ProductPriceSyncResultType
                        .StoreLocationNotFound
            };
        }

        // -------------------------------------------------
        // 3. Find the retailer that owns the store
        // -------------------------------------------------

        var retailer =
            await _retailerRepository.GetByIdAsync(
                store.RetailerId
            );

        if (retailer is null)
        {
            return new ProductPriceSyncResult
            {
                Result =
                    ProductPriceSyncResultType
                        .RetailerNotFound
            };
        }

        // -------------------------------------------------
        // 4. Find the provider that supports this retailer
        // -------------------------------------------------

        var provider =
            _providerResolver.Resolve(
                retailer.Name
            );

        if (provider is null)
        {
            return new ProductPriceSyncResult
            {
                Result =
                    ProductPriceSyncResultType
                        .ProviderNotFound
            };
        }

        // -------------------------------------------------
        // 5. Ask the provider for authoritative
        //    product information
        // -------------------------------------------------

        var providerProduct =
            await provider.GetProductAsync(
                retailer.Name,
                store.ExternalLocationId,
                externalProductId.Trim()
            );

        if (providerProduct is null)
        {
            return new ProductPriceSyncResult
            {
                Result =
                    ProductPriceSyncResultType
                        .ProviderProductNotFound,

                ProviderName =
                    provider.ProviderName
            };
        }

        // -------------------------------------------------
        // 6. Evaluate how strongly this provider product
        //    matches the canonical Item
        //
        // IMPORTANT:
        // matchResult is declared here so it remains
        // available when we create the RetailerProduct.
        // -------------------------------------------------

        var matchResult =
            await _productMatchingService
                .EvaluateAsync(
                    item,
                    providerProduct
                );

        // -------------------------------------------------
        // 7. See whether this retailer product already
        //    exists in our database
        // -------------------------------------------------

        var existingRetailerProduct =
            await _retailerProductRepository
                .GetByRetailerAndExternalIdAsync(
                    retailer.Id,
                    providerProduct.ExternalProductId
                );

        RetailerProduct retailerProduct;

        var productCreated = false;
        var productUpdated = false;

        // -------------------------------------------------
        // 8a. Create the RetailerProduct if it is new
        // -------------------------------------------------

        if (existingRetailerProduct is null)
        {
            retailerProduct =
                new RetailerProduct
                {
                    ItemId =
                        item.Id,

                    RetailerId =
                        retailer.Id,

                    ExternalProductId =
                        providerProduct.ExternalProductId,

                    Name =
                        providerProduct.Name,

                    Brand =
                        providerProduct.Brand,

                    Size =
                        providerProduct.Size,

                    Upc =
                        providerProduct.Upc,

                    MatchMethod =
                        matchResult.IsMatch &&
                        matchResult.MatchMethod.HasValue
                            ? matchResult.MatchMethod.Value
                            : ProductMatchMethod.Manual,

                    MatchConfidence =
                        matchResult.IsMatch
                            ? matchResult.MatchConfidence
                            : null,

                    IsActive =
                        true,

                    LastSeenAt =
                        DateTime.UtcNow
                };

            await _retailerProductRepository
                .AddAsync(
                    retailerProduct
                );

            productCreated = true;
        }

        // -------------------------------------------------
        // 8b. Otherwise refresh the existing
        //     RetailerProduct
        // -------------------------------------------------

        else
        {
            // The same retailer product must not silently
            // move from one canonical Item to another.
            if (
                existingRetailerProduct.ItemId !=
                item.Id)
            {
                return new ProductPriceSyncResult
                {
                    Result =
                        ProductPriceSyncResultType
                            .ProductMatchedToDifferentItem,

                    RetailerProduct =
                        existingRetailerProduct,

                    ProviderName =
                        provider.ProviderName
                };
            }

            retailerProduct =
                existingRetailerProduct;

            retailerProduct.Name =
                providerProduct.Name;

            retailerProduct.Brand =
                providerProduct.Brand;

            retailerProduct.Size =
                providerProduct.Size;

            retailerProduct.Upc =
                providerProduct.Upc;

            retailerProduct.IsActive =
                true;

            retailerProduct.LastSeenAt =
                DateTime.UtcNow;

            // We intentionally do NOT overwrite
            // MatchMethod or MatchConfidence here.
            //
            // If this product was originally established
            // manually, for example, refreshing its
            // metadata should not rewrite that history.

            await _retailerProductRepository
                .UpdateAsync(
                    retailerProduct
                );

            productUpdated = true;
        }

        // -------------------------------------------------
        // 9. Ask the provider for the current price
        //    at this specific physical store
        // -------------------------------------------------

        var providerPrice =
            await provider.GetPriceAsync(
                retailer.Name,
                store.ExternalLocationId,
                providerProduct.ExternalProductId
            );

        if (providerPrice is null)
        {
            return new ProductPriceSyncResult
            {
                Result =
                    ProductPriceSyncResultType
                        .PriceNotAvailable,

                RetailerProduct =
                    retailerProduct,

                ProductCreated =
                    productCreated,

                ProductUpdated =
                    productUpdated,

                PriceSynced =
                    false,

                ProviderName =
                    provider.ProviderName
            };
        }

        // -------------------------------------------------
        // 10. Store or update the Price row
        // -------------------------------------------------

        var priceResult =
            await _priceService.UpsertAsync(
                retailerProduct.Id,
                store.Id,
                providerPrice.RegularPrice,
                providerPrice.SalePrice,
                providerPrice.MemberPrice,
                providerPrice.AvailabilityStatus,
                provider.ProviderName,
                providerPrice.SourceUpdatedAt
            );

        // -------------------------------------------------
        // 11. Make sure the Price upsert succeeded
        // -------------------------------------------------

        if (
            priceResult.Result !=
                PriceUpsertResultType.Created &&
            priceResult.Result !=
                PriceUpsertResultType.Updated)
        {
            return new ProductPriceSyncResult
            {
                Result =
                    ProductPriceSyncResultType
                        .PriceSyncFailed,

                RetailerProduct =
                    retailerProduct,

                ProductCreated =
                    productCreated,

                ProductUpdated =
                    productUpdated,

                PriceSynced =
                    false,

                ProviderName =
                    provider.ProviderName
            };
        }

        // -------------------------------------------------
        // 12. Everything succeeded
        // -------------------------------------------------

        return new ProductPriceSyncResult
        {
            Result =
                ProductPriceSyncResultType
                    .Succeeded,

            RetailerProduct =
                retailerProduct,

            Price =
                priceResult.StoredPrice,

            ProductCreated =
                productCreated,

            ProductUpdated =
                productUpdated,

            PriceSynced =
                true,

            ProviderName =
                provider.ProviderName
        };
    }
}