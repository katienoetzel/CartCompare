using System.Text.Json;
using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class ComparisonPriceRefreshService : IComparisonPriceRefreshService
{
    public static readonly TimeSpan PriceLifetime = TimeSpan.FromHours(6);
    // Beyond this many safe matches the search is too broad to trust.
    private const int MaxAutomaticMatches = 3;
    private readonly IPriceService _prices;
    private readonly IProductCandidateService _candidates;
    private readonly IProductPriceSyncService _sync;

    public ComparisonPriceRefreshService(
        IPriceService prices, IProductCandidateService candidates,
        IProductPriceSyncService sync)
    {
        _prices = prices;
        _candidates = candidates;
        _sync = sync;
    }

    public async Task<ComparisonPriceRefreshResult> RefreshAsync(
        Item item, StoreLocation store, List<RetailerProduct> matchedProducts)
    {
        var result = new ComparisonPriceRefreshResult();
        if (matchedProducts.Count == 0)
            return await DiscoverAsync(item, store);

        foreach (var product in matchedProducts)
        {
            var saved = await _prices.GetByProductAndStoreAsync(product.Id, store.Id);
            var now = DateTime.UtcNow;
            var fresh = saved is not null && saved.LastCheckedAt <= now &&
                now - saved.LastCheckedAt < PriceLifetime;
            if (fresh && saved!.AvailabilityStatus == AvailabilityStatus.Unavailable)
            {
                SetFailure(result, "unavailable");
                continue;
            }
            if (fresh && HasPrice(saved!))
            {
                result.UsableProductIds.Add(product.Id);
                if (result.Message is null) result.Status = "cached";
                continue;
            }
            try
            {
                // Existing links include products approved manually; preserve them.
                var synced = await _sync.SyncAsync(item.Id, store.Id, product.ExternalProductId);
                if (synced.Result == ProductPriceSyncResultType.ProviderProductNotFound)
                {
                    var recovered = await DiscoverAsync(item, store, product);
                    result.UsableProductIds.UnionWith(recovered.UsableProductIds);
                    if (recovered.Message is not null) SetFailure(result, recovered.Status);
                    else if (result.Message is null) result.Status = recovered.Status;
                }
                else ApplySync(result, synced);
            }
            catch (Exception exception) when (IsProviderFailure(exception))
            {
                SetFailure(result, "provider_error");
            }
        }
        if (result.UsableProductIds.Count > 0 && result.Message is not null)
        {
            result.Status = "partial_refresh";
            result.Message = "Some matched product prices could not be confirmed. Showing verified prices.";
        }
        return result;
    }

    private async Task<ComparisonPriceRefreshResult> DiscoverAsync(
        Item item, StoreLocation store, RetailerProduct? previous = null)
    {
        try
        {
            // Searching the saved name repopulates providers that cannot fetch
            // a product by ID alone. Never infer that an ID lookup proves absence.
            var search = await _candidates.FindCandidatesAsync(item.Id, store.Id, previous?.Name);
            if (search.Result != ProductCandidateSearchResultType.Succeeded)
                return Failure(search.Result == ProductCandidateSearchResultType.ProviderNotFound
                    ? "provider_unavailable" : "lookup_failed");

            if (previous is not null && search.Candidates.Any(candidate =>
                candidate.ExternalProductId == previous.ExternalProductId))
            {
                var recovered = new ComparisonPriceRefreshResult();
                ApplySync(recovered, await _sync.SyncAsync(item.Id, store.Id, previous.ExternalProductId));
                return recovered;
            }
            // If the saved product name was too specific, try the canonical name.
            if (previous is not null && previous.Name != item.Name)
                search = await _candidates.FindCandidatesAsync(item.Id, store.Id);
            if (search.Result != ProductCandidateSearchResultType.Succeeded)
                return Failure(search.Result == ProductCandidateSearchResultType.ProviderNotFound
                    ? "provider_unavailable" : "lookup_failed");

            var safe = search.Candidates.Where(candidate =>
                    AutomaticProductMatchPolicy.IsSafe(item, candidate))
                .GroupBy(candidate => candidate.ExternalProductId)
                .Select(group => group.First()).ToList();
            var upc = safe.Where(candidate => candidate.MatchMethod == ProductMatchMethod.Upc).ToList();
            if (upc.Count > 0) safe = upc;
            if (safe.Count == 0)
                return Failure(search.Candidates.Count == 0 ? "no_match" : "match_uncertain");
            if (safe.Count > MaxAutomaticMatches) return Failure("ambiguous_match");

            // Every safe match is an acceptable product, so link them all;
            // the comparison then prices the item at the cheapest one.
            var result = new ComparisonPriceRefreshResult();
            foreach (var candidate in safe)
                ApplySync(result, await _sync.SyncMatchedAsync(item.Id, store.Id, candidate.ExternalProductId));
            if (result.UsableProductIds.Count > 0 && result.Message is not null)
            {
                result.Status = "refreshed";
                result.Message = null;
            }
            return result;
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            return Failure("provider_error");
        }
    }

    private static bool HasPrice(Price price) =>
        price.RegularPrice.HasValue || price.SalePrice.HasValue || price.MemberPrice.HasValue;

    private static bool IsProviderFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or JsonException;

    private static void ApplySync(ComparisonPriceRefreshResult result, ProductPriceSyncResult synced)
    {
        if (synced.Result == ProductPriceSyncResultType.Succeeded &&
            synced.RetailerProduct is not null && synced.Price is not null)
        {
            if (synced.Price.AvailabilityStatus == AvailabilityStatus.Unavailable)
                SetFailure(result, "unavailable");
            else if (!HasPrice(synced.Price))
                SetFailure(result, "price_unavailable");
            else
            {
                result.UsableProductIds.Add(synced.RetailerProduct.Id);
                if (result.Message is null) result.Status = "refreshed";
            }
            return;
        }
        SetFailure(result, synced.Result switch
        {
            ProductPriceSyncResultType.ProviderNotFound => "provider_unavailable",
            ProductPriceSyncResultType.ProviderProductNotFound => "product_not_found",
            ProductPriceSyncResultType.PriceNotAvailable => "price_unavailable",
            ProductPriceSyncResultType.ProductMatchedToDifferentItem => "product_conflict",
            ProductPriceSyncResultType.AutomaticMatchRejected => "match_uncertain",
            _ => "sync_failed"
        });
    }

    private static ComparisonPriceRefreshResult Failure(string status)
    {
        var result = new ComparisonPriceRefreshResult();
        SetFailure(result, status);
        return result;
    }

    private static void SetFailure(ComparisonPriceRefreshResult result, string status)
    {
        result.Status = status;
        result.Message = status switch
        {
            "no_match" => "No product was returned by the retailer search.",
            "match_uncertain" => "A matching product could not be confirmed automatically.",
            "ambiguous_match" => "Several products could match; a product selection is needed.",
            "unavailable" => "The matched product is unavailable at this store.",
            "price_unavailable" => "The retailer did not provide a usable price.",
            "provider_unavailable" => "Automatic price lookup is not available for this retailer.",
            "product_not_found" => "The retailer could not find the previously matched product.",
            "product_conflict" => "This retailer product is already linked to another item.",
            _ => "Price lookup failed. Try comparing again."
        };
    }
}
