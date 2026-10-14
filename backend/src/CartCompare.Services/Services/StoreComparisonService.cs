
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class StoreComparisonService : IStoreComparisonService
{
    private readonly IGroceryListRepository
        _groceryListRepository;

    private readonly IItemRepository
        _itemRepository;

    private readonly IStoreLocationRepository
        _storeLocationRepository;

    private readonly IRetailerProductRepository
        _retailerProductRepository;

    private readonly IPriceService _priceService;
    private readonly IComparisonPriceRefreshService _priceRefresh;


    public StoreComparisonService(
        IGroceryListRepository groceryListRepository,
        IItemRepository itemRepository,
        IStoreLocationRepository storeLocationRepository,
        IRetailerProductRepository retailerProductRepository,
        IPriceService priceService,
        IComparisonPriceRefreshService priceRefresh)
    {
        _groceryListRepository = groceryListRepository;
        _itemRepository = itemRepository;
        _storeLocationRepository = storeLocationRepository;
        _retailerProductRepository = retailerProductRepository;
        _priceService = priceService;
        _priceRefresh = priceRefresh;
    }

    // The legacy GET endpoint remains a read of saved prices.
    public Task<StoreComparisonResult?> CompareStoreAsync(int userId, int storeLocationId) =>
        CompareStoreCoreAsync(userId, storeLocationId, false);

    private async Task<StoreComparisonResult?> CompareStoreCoreAsync(
        int userId, int storeLocationId, bool refreshPrices)
    {
        var store =
            await _storeLocationRepository.GetByIdAsync(
                storeLocationId
            );

        if (store is null || !store.IsActive)
        {
            return null;
        }

        var groceryListItems =
            await _groceryListRepository.GetByUserIdAsync(
                userId
            );

        var result = new StoreComparisonResult
        {
            StoreLocationId = store.Id,
            StoreName = store.Name
                ?? $"{store.AddressLine1}, {store.City}",
            RetailerId = store.RetailerId,
            KnownSubtotal = 0m,
            IsComplete = true
        };

        foreach (var groceryListItem in groceryListItems)
        {
            var item =
                await _itemRepository.GetByIdAsync(
                    groceryListItem.ItemId
                );

            if (item is null)
            {
                result.IsComplete = false;

                result.Items.Add(
                    new StoreComparisonItemResult
                    {
                        ItemId = groceryListItem.ItemId,
                        ItemName = "Unknown item",
                        Quantity = groceryListItem.Quantity,
                        IsAvailable = false
                    }
                );

                continue;
            }

            var retailerProducts =
                await _retailerProductRepository
                    .GetByItemIdAsync(item.Id);

            var storeRetailerProducts =
                retailerProducts
                    .Where(p =>
                        p.RetailerId == store.RetailerId && p.IsActive)
                    .ToList();

            var refresh = new ComparisonPriceRefreshResult { Status = "saved" };
            if (refreshPrices)
            {
                refresh = await _priceRefresh.RefreshAsync(item, store, storeRetailerProducts);
                // A newly discovered link was persisted by the sync service.
                retailerProducts = await _retailerProductRepository.GetByItemIdAsync(item.Id);
                storeRetailerProducts = retailerProducts.Where(product =>
                    product.RetailerId == store.RetailerId && product.IsActive &&
                    refresh.UsableProductIds.Contains(product.Id)).ToList();
            }

            StoreComparisonItemResult?
                bestItemResult = null;

            foreach (var retailerProduct
                in storeRetailerProducts)
            {
                var effectivePrice =
    await _priceService.GetEffectivePriceAsync(
        userId,
        retailerProduct.Id,
        store.Id
    );

                if (
                    effectivePrice is null ||
                    effectivePrice.Amount is null ||
                    effectivePrice.PriceType is null)
                {
                    continue;
                }

                // A per-lb item is bought by the pound. A weighed product is
                // already priced per pound; a fixed package is converted.
                var unitPrice = effectivePrice.Amount.Value;
                if (ProductSize.IsPerPound(item.Size) &&
                    !ProductSize.IsPerPound(retailerProduct.Size))
                {
                    var pounds = ProductSize.Pounds(retailerProduct.Size);
                    if (pounds is null or <= 0) continue;
                    unitPrice = decimal.Round(unitPrice / pounds.Value, 2);
                }

                var lineTotal =
                    unitPrice *
                    groceryListItem.Quantity;

                if (
                    bestItemResult is null ||
                    lineTotal <
                        bestItemResult.LineTotal)
                {
                    bestItemResult =
                        new StoreComparisonItemResult
                        {
                            ItemId = item.Id,
                            ItemName = item.Name,
                            Quantity =
                                groceryListItem.Quantity,

                            RetailerProductId =
                                retailerProduct.Id,

                            RetailerProductName =
                                retailerProduct.Name,

                            UnitPrice =
                                unitPrice,

                            PriceType =
                                effectivePrice.PriceType.Value,

                            LineTotal =
                                lineTotal,

                            IsAvailable = true,
                            PriceStatus = refresh.Status,
                            PriceMessage = refresh.Message,
                            PriceLastCheckedAt = effectivePrice.LastCheckedAt
                        };
                }
            }

            if (bestItemResult is null)
            {
                result.IsComplete = false;

                result.Items.Add(
                    new StoreComparisonItemResult
                    {
                        ItemId = item.Id,
                        ItemName = item.Name,
                        Quantity = groceryListItem.Quantity,
                        IsAvailable = false,
                        PriceStatus = refresh.Status is "cached" or "refreshed"
                            ? "price_unavailable" : refresh.Status,
                        PriceMessage = refresh.Message ?? "No price is available for this item."
                    }
                );

                continue;
            }

            result.Items.Add(bestItemResult);

            result.KnownSubtotal +=
                bestItemResult.LineTotal!.Value;
        }

        return result;
    }
    public async Task<MultiStoreComparisonResult>
    CompareStoresAsync(
        int userId,
        List<int> storeLocationIds)
    {
        var comparisons =
            new List<StoreComparisonResult>();

        var missingStoreIds =
            new List<int>();

        var distinctStoreIds =
            storeLocationIds.Distinct().ToList();

        foreach (var storeLocationId in distinctStoreIds)
        {
            var comparison =
                await CompareStoreCoreAsync(
                    userId,
                    storeLocationId,
                    true
                );

            if (comparison is null)
            {
                missingStoreIds.Add(storeLocationId);
                continue;
            }

            comparisons.Add(comparison);
        }

        var completeStores =
            comparisons
                .Where(c => c.IsComplete)
                .OrderBy(c => c.KnownSubtotal)
                .ThenBy(c => c.StoreName)
                .ToList();

        decimal? previousSubtotal = null;
        var currentRank = 0;

        for (var index = 0;
             index < completeStores.Count;
             index++)
        {
            var store = completeStores[index];

            if (
                previousSubtotal is null ||
                store.KnownSubtotal != previousSubtotal.Value)
            {
                currentRank = index + 1;
            }

            store.Rank = currentRank;
            previousSubtotal = store.KnownSubtotal;
        }

        var incompleteStores =
            comparisons
                .Where(c => !c.IsComplete)
                .OrderBy(c => c.MissingItemCount)
                .ThenBy(c => c.StoreName)
                .ToList();

        foreach (var store in incompleteStores)
        {
            store.Rank = null;
        }

        return new MultiStoreComparisonResult
        {
            Stores = completeStores
                .Concat(incompleteStores)
                .ToList(),

            MissingStoreLocationIds =
                missingStoreIds,

            CompleteStoreCount =
                completeStores.Count,

            IncompleteStoreCount =
                incompleteStores.Count
        };
    }
}