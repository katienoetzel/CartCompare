using CartCompare.Providers.Models;

namespace CartCompare.Providers.Interfaces;

public interface IPriceProvider
{
    string ProviderName { get; }

    bool SupportsRetailer(string retailerName);

    Task<List<ProviderStoreLocation>> FindStoresAsync(
        string retailerName,
        string postalCode
    );

    Task<List<ProviderProduct>> SearchProductsAsync(
        string retailerName,
        string externalLocationId,
        string query
    );

    Task<ProviderProduct?> GetProductAsync(
        string retailerName,
        string externalLocationId,
        string externalProductId
    );

    Task<ProviderPriceSnapshot?> GetPriceAsync(
        string retailerName,
        string externalLocationId,
        string externalProductId
    );
}