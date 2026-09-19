
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


    public StoreComparisonService(
        IGroceryListRepository groceryListRepository,
        IItemRepository itemRepository,
        IStoreLocationRepository storeLocationRepository,
        IRetailerProductRepository retailerProductRepository,
        IPriceService priceService)
    {
        _groceryListRepository = groceryListRepository;
        _itemRepository = itemRepository;
        _storeLocationRepository = storeLocationRepository;
        _retailerProductRepository = retailerProductRepository;
        _priceService = priceService;
    }

    public async Task<StoreComparisonResult?> CompareStoreAsync(
        int userId,
        int storeLocationId)
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

                var lineTotal =
                    effectivePrice.Amount.Value *
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
                                effectivePrice.Amount.Value,

                            PriceType =
                                effectivePrice.PriceType.Value,

                            LineTotal =
                                lineTotal,

                            IsAvailable = true
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
                        IsAvailable = false
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
                await CompareStoreAsync(
                    userId,
                    storeLocationId
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