using System.Diagnostics;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;
using Microsoft.Extensions.Logging;

namespace CartCompare.Infrastructure.Providers;

// Wraps a retailer provider so every outbound call is logged with
// structured fields: provider, operation, store, duration and outcome.
// Failures are logged as warnings and rethrown unchanged. Postal codes
// are not logged.
public class LoggingPriceProvider : IPriceProvider
{
    private readonly IPriceProvider _inner;
    private readonly ILogger<LoggingPriceProvider> _logger;

    public LoggingPriceProvider(
        IPriceProvider inner,
        ILogger<LoggingPriceProvider> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public string ProviderName => _inner.ProviderName;

    public bool SupportsRetailer(string retailerName) =>
        _inner.SupportsRetailer(retailerName);

    public Task<List<ProviderStoreLocation>> FindStoresAsync(
        string retailerName, string postalCode) =>
        LogAsync(
            "FindStores", retailerName, externalLocationId: null,
            () => _inner.FindStoresAsync(retailerName, postalCode),
            stores => $"{stores.Count} stores");

    public Task<List<ProviderProduct>> SearchProductsAsync(
        string retailerName, string externalLocationId, string query) =>
        LogAsync(
            "SearchProducts", retailerName, externalLocationId,
            () => _inner.SearchProductsAsync(retailerName, externalLocationId, query),
            products => $"{products.Count} products");

    public Task<ProviderProduct?> GetProductAsync(
        string retailerName, string externalLocationId, string externalProductId) =>
        LogAsync(
            "GetProduct", retailerName, externalLocationId,
            () => _inner.GetProductAsync(retailerName, externalLocationId, externalProductId),
            product => product is null ? "not found" : "found");

    public Task<ProviderPriceSnapshot?> GetPriceAsync(
        string retailerName, string externalLocationId, string externalProductId) =>
        LogAsync(
            "GetPrice", retailerName, externalLocationId,
            () => _inner.GetPriceAsync(retailerName, externalLocationId, externalProductId),
            price => price is null ? "no price" : price.AvailabilityStatus.ToString());

    private async Task<T> LogAsync<T>(
        string operation,
        string retailerName,
        string? externalLocationId,
        Func<Task<T>> call,
        Func<T, string> describe)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await call();

            _logger.LogInformation(
                "Provider {Provider} {Operation} for {Retailer} at {Location} returned {Outcome} in {DurationMs} ms",
                _inner.ProviderName, operation, retailerName, externalLocationId,
                describe(result), stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Provider {Provider} {Operation} for {Retailer} at {Location} failed after {DurationMs} ms",
                _inner.ProviderName, operation, retailerName, externalLocationId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
