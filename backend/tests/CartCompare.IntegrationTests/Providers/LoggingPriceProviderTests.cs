using CartCompare.Infrastructure.Providers;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;
using Microsoft.Extensions.Logging;

namespace CartCompare.IntegrationTests.Providers;

public class LoggingPriceProviderTests
{
    private readonly RecordingLogger _logger = new();

    [Fact]
    public async Task SuccessfulCall_IsLoggedWithStructuredFields()
    {
        var provider = new LoggingPriceProvider(new StubProvider(), _logger);

        var products = await provider.SearchProductsAsync("Harris Teeter", "09700304", "whole milk");

        Assert.Equal(2, products.Count);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Stub", entry.Fields["Provider"]);
        Assert.Equal("SearchProducts", entry.Fields["Operation"]);
        Assert.Equal("Harris Teeter", entry.Fields["Retailer"]);
        Assert.Equal("09700304", entry.Fields["Location"]);
        Assert.Equal("2 products", entry.Fields["Outcome"]);
        Assert.True(entry.Fields.ContainsKey("DurationMs"));
    }

    [Fact]
    public async Task FailedCall_IsLoggedAsWarningAndRethrown()
    {
        var provider = new LoggingPriceProvider(new StubProvider(), _logger);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GetPriceAsync("Harris Teeter", "09700304", "milk"));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<HttpRequestException>(entry.Exception);
        Assert.Equal("GetPrice", entry.Fields["Operation"]);
    }

    [Fact]
    public async Task PostalCodes_AreNotLogged()
    {
        var provider = new LoggingPriceProvider(new StubProvider(), _logger);

        await provider.FindStoresAsync("Harris Teeter", "27613");

        var entry = Assert.Single(_logger.Entries);
        Assert.DoesNotContain("27613", entry.Message);
        Assert.DoesNotContain(entry.Fields.Values, value => Equals(value, "27613"));
    }

    private sealed class StubProvider : IPriceProvider
    {
        public string ProviderName => "Stub";

        public bool SupportsRetailer(string retailerName) => true;

        public Task<List<ProviderStoreLocation>> FindStoresAsync(string retailerName, string postalCode) =>
            Task.FromResult(new List<ProviderStoreLocation>());

        public Task<List<ProviderProduct>> SearchProductsAsync(
            string retailerName, string externalLocationId, string query) =>
            Task.FromResult(new List<ProviderProduct>
            {
                new() { ExternalProductId = "1", Name = "Whole Milk" },
                new() { ExternalProductId = "2", Name = "Vitamin D Milk" }
            });

        public Task<ProviderProduct?> GetProductAsync(
            string retailerName, string externalLocationId, string externalProductId) =>
            Task.FromResult<ProviderProduct?>(null);

        public Task<ProviderPriceSnapshot?> GetPriceAsync(
            string retailerName, string externalLocationId, string externalProductId) =>
            throw new HttpRequestException("Provider is down");
    }

    private sealed record LogEntry(
        LogLevel Level, string Message, Exception? Exception, Dictionary<string, object?> Fields);

    private sealed class RecordingLogger : ILogger<LoggingPriceProvider>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();

            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, fields));
        }
    }
}
