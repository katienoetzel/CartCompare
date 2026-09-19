using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;

namespace CartCompare.IntegrationTests.Fakes;

public class FakePriceProvider
    : IPriceProvider
{
    public string ProviderName =>
        "FakePriceProvider";

    public HashSet<string> SupportedRetailers { get; } =
        new(
            StringComparer.OrdinalIgnoreCase
        );

    public List<ProviderStoreLocation>
        StoresToReturn
    { get; set; } =
            new();

    public List<ProviderProduct>
        ProductsToReturn
    { get; set; } =
            new();

    public Dictionary<string, ProviderProduct>
        ProductsByExternalId
    { get; } =
            new(
                StringComparer.OrdinalIgnoreCase
            );

    public Dictionary<string, ProviderPriceSnapshot>
        PricesByExternalProductId
    { get; } =
            new(
                StringComparer.OrdinalIgnoreCase
            );

    // -----------------------------------------
    // Optional handlers
    //
    // Most tests can just fill the collections
    // above. These handlers let a future test
    // customize behavior when necessary.
    // -----------------------------------------

    public Func<
        string,
        string,
        Task<List<ProviderStoreLocation>>
    >? FindStoresHandler
    { get; set; }

    public Func<
        string,
        string,
        string,
        Task<List<ProviderProduct>>
    >? SearchProductsHandler
    { get; set; }

    public Func<
        string,
        string,
        string,
        Task<ProviderProduct?>
    >? GetProductHandler
    { get; set; }

    public Func<
        string,
        string,
        string,
        Task<ProviderPriceSnapshot?>
    >? GetPriceHandler
    { get; set; }

    // -----------------------------------------
    // Call history
    //
    // These let integration tests prove that
    // the application called the provider with
    // the correct retailer/location/query IDs.
    // -----------------------------------------

    public List<(
        string RetailerName,
        string PostalCode
    )> FindStoresCalls
    { get; } =
        new();

    public List<(
        string RetailerName,
        string ExternalLocationId,
        string Query
    )> SearchProductsCalls
    { get; } =
        new();

    public List<(
        string RetailerName,
        string ExternalLocationId,
        string ExternalProductId
    )> GetProductCalls
    { get; } =
        new();

    public List<(
        string RetailerName,
        string ExternalLocationId,
        string ExternalProductId
    )> GetPriceCalls
    { get; } =
        new();

    public bool SupportsRetailer(
        string retailerName)
    {
        return SupportedRetailers.Contains(
            retailerName
        );
    }

    public async Task<List<ProviderStoreLocation>>
        FindStoresAsync(
            string retailerName,
            string postalCode)
    {
        FindStoresCalls.Add(
            (
                retailerName,
                postalCode
            )
        );

        if (FindStoresHandler is not null)
        {
            return await FindStoresHandler(
                retailerName,
                postalCode
            );
        }

        return StoresToReturn
            .ToList();
    }

    public async Task<List<ProviderProduct>>
        SearchProductsAsync(
            string retailerName,
            string externalLocationId,
            string query)
    {
        SearchProductsCalls.Add(
            (
                retailerName,
                externalLocationId,
                query
            )
        );

        if (SearchProductsHandler is not null)
        {
            return await SearchProductsHandler(
                retailerName,
                externalLocationId,
                query
            );
        }

        return ProductsToReturn
            .ToList();
    }

    public async Task<ProviderProduct?>
        GetProductAsync(
            string retailerName,
            string externalLocationId,
            string externalProductId)
    {
        GetProductCalls.Add(
            (
                retailerName,
                externalLocationId,
                externalProductId
            )
        );

        if (GetProductHandler is not null)
        {
            return await GetProductHandler(
                retailerName,
                externalLocationId,
                externalProductId
            );
        }

        if (
            ProductsByExternalId.TryGetValue(
                externalProductId,
                out var product
            )
        )
        {
            return product;
        }

        return ProductsToReturn
            .FirstOrDefault(
                candidate =>
                    string.Equals(
                        candidate.ExternalProductId,
                        externalProductId,
                        StringComparison.OrdinalIgnoreCase
                    )
            );
    }

    public async Task<ProviderPriceSnapshot?>
        GetPriceAsync(
            string retailerName,
            string externalLocationId,
            string externalProductId)
    {
        GetPriceCalls.Add(
            (
                retailerName,
                externalLocationId,
                externalProductId
            )
        );

        if (GetPriceHandler is not null)
        {
            return await GetPriceHandler(
                retailerName,
                externalLocationId,
                externalProductId
            );
        }

        if (
            PricesByExternalProductId.TryGetValue(
                externalProductId,
                out var price
            )
        )
        {
            return price;
        }

        return null;
    }

    public void Reset()
    {
        SupportedRetailers.Clear();

        StoresToReturn.Clear();
        ProductsToReturn.Clear();

        ProductsByExternalId.Clear();
        PricesByExternalProductId.Clear();

        FindStoresCalls.Clear();
        SearchProductsCalls.Clear();
        GetProductCalls.Clear();
        GetPriceCalls.Clear();

        FindStoresHandler =
            null;

        SearchProductsHandler =
            null;

        GetProductHandler =
            null;

        GetPriceHandler =
            null;
    }
}