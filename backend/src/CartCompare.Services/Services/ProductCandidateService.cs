using CartCompare.Providers.Interfaces;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class ProductCandidateService
    : IProductCandidateService
{
    private readonly IItemRepository
        _itemRepository;

    private readonly IStoreLocationRepository
        _storeLocationRepository;

    private readonly IRetailerRepository
        _retailerRepository;

    private readonly IPriceProviderResolver
        _providerResolver;

    private readonly IProductMatchingService
        _productMatchingService;

    public ProductCandidateService(
        IItemRepository itemRepository,
        IStoreLocationRepository storeLocationRepository,
        IRetailerRepository retailerRepository,
        IPriceProviderResolver providerResolver,
        IProductMatchingService productMatchingService)
    {
        _itemRepository =
            itemRepository;

        _storeLocationRepository =
            storeLocationRepository;

        _retailerRepository =
            retailerRepository;

        _providerResolver =
            providerResolver;

        _productMatchingService =
            productMatchingService;
    }

    public async Task<ProductCandidateSearchResult>
        FindCandidatesAsync(
            int itemId,
            int storeLocationId,
            string? query = null)
    {
        // ---------------------------------------------
        // 1. Find our canonical Item
        // ---------------------------------------------

        var item =
            await _itemRepository.GetByIdAsync(
                itemId
            );

        if (item is null)
        {
            return new ProductCandidateSearchResult
            {
                Result =
                    ProductCandidateSearchResultType
                        .ItemNotFound,

                ItemId =
                    itemId,

                StoreLocationId =
                    storeLocationId
            };
        }

        // ---------------------------------------------
        // 2. Find the physical store
        // ---------------------------------------------

        var store =
            await _storeLocationRepository
                .GetByIdAsync(
                    storeLocationId
                );

        if (store is null)
        {
            return new ProductCandidateSearchResult
            {
                Result =
                    ProductCandidateSearchResultType
                        .StoreLocationNotFound,

                ItemId =
                    itemId,

                StoreLocationId =
                    storeLocationId
            };
        }

        // ---------------------------------------------
        // 3. Find the retailer for that store
        // ---------------------------------------------

        var retailer =
            await _retailerRepository.GetByIdAsync(
                store.RetailerId
            );

        if (retailer is null)
        {
            return new ProductCandidateSearchResult
            {
                Result =
                    ProductCandidateSearchResultType
                        .RetailerNotFound,

                ItemId =
                    itemId,

                StoreLocationId =
                    storeLocationId
            };
        }

        // ---------------------------------------------
        // 4. Find a provider that supports retailer
        // ---------------------------------------------

        var provider =
            _providerResolver.Resolve(
                retailer.Name
            );

        if (provider is null)
        {
            return new ProductCandidateSearchResult
            {
                Result =
                    ProductCandidateSearchResultType
                        .ProviderNotFound,

                ItemId =
                    itemId,

                StoreLocationId =
                    storeLocationId,

                RetailerName =
                    retailer.Name
            };
        }

        // ---------------------------------------------
        // 5. Decide what text to search for
        // ---------------------------------------------

        var searchQuery =
            string.IsNullOrWhiteSpace(query)
                ? item.Name.Trim()
                : query.Trim();

        // ---------------------------------------------
        // 6. Ask the provider for products
        // ---------------------------------------------

        var providerProducts =
            await provider.SearchProductsAsync(
                retailer.Name,
                store.ExternalLocationId,
                searchQuery
            );

        var candidates =
            new List<ProductCandidate>();

        // ---------------------------------------------
        // 7. Evaluate every returned provider product
        // ---------------------------------------------

        foreach (var providerProduct in providerProducts)
        {
            var matchResult =
                await _productMatchingService
                    .EvaluateAsync(
                        item,
                        providerProduct
                    );

            candidates.Add(
                new ProductCandidate
                {
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

                    IsMatch =
                        matchResult.IsMatch,

                    MatchMethod =
                        matchResult.MatchMethod,

                    MatchConfidence =
                        matchResult.MatchConfidence,

                    MatchReason =
                        matchResult.Reason
                }
            );
        }

        // ---------------------------------------------
        // 8. Put strongest matches at the top
        // ---------------------------------------------

        candidates =
            candidates
                .OrderByDescending(
                    candidate =>
                        candidate.IsMatch
                )
                .ThenByDescending(
                    candidate =>
                        candidate.MatchConfidence
                        ?? -1m
                )
                .ThenBy(
                    candidate =>
                        candidate.Name
                )
                .ToList();

        // ---------------------------------------------
        // 9. Return normalized results
        // ---------------------------------------------

        return new ProductCandidateSearchResult
        {
            Result =
                ProductCandidateSearchResultType
                    .Succeeded,

            ItemId =
                item.Id,

            StoreLocationId =
                store.Id,

            RetailerName =
                retailer.Name,

            ProviderName =
                provider.ProviderName,

            SearchQuery =
                searchQuery,

            Candidates =
                candidates
        };
    }
}