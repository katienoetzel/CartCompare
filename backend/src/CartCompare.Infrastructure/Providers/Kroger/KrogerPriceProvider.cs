using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CartCompare.Entities.Enums;
using CartCompare.Infrastructure.Providers.Kroger.Models;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;

namespace CartCompare.Infrastructure.Providers.Kroger;

public class KrogerPriceProvider : IPriceProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KrogerTokenService _tokenService;

    public KrogerPriceProvider(
        IHttpClientFactory httpClientFactory,
        KrogerTokenService tokenService)
    {
        _httpClientFactory = httpClientFactory;
        _tokenService = tokenService;
    }

    public string ProviderName => "KrogerPublicApi";

    public bool SupportsRetailer(string retailerName)
    {
        return string.Equals(
            retailerName,
            "Kroger",
            StringComparison.OrdinalIgnoreCase
        );
    }

    public async Task<List<ProviderStoreLocation>>
        FindStoresAsync(
            string retailerName,
            string postalCode)
    {
        if (!SupportsRetailer(retailerName))
        {
            return new List<ProviderStoreLocation>();
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return new List<ProviderStoreLocation>();
        }

        var accessToken =
            await _tokenService.GetAccessTokenAsync();

        var encodedPostalCode =
            Uri.EscapeDataString(postalCode.Trim());

        var encodedChain =
            Uri.EscapeDataString(retailerName.Trim());

        var requestUrl =
            $"v1/locations" +
            $"?filter.zipCode.near={encodedPostalCode}" +
            $"&filter.limit=50" +
            $"&filter.chain={encodedChain}";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                requestUrl
            );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        var httpClient =
            _httpClientFactory.CreateClient("Kroger");

        using var response =
            await httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var krogerResponse =
            await response.Content
                .ReadFromJsonAsync<KrogerLocationListResponse>();

        var results =
            new List<ProviderStoreLocation>();

        if (krogerResponse?.Data is null)
        {
            return results;
        }

        foreach (var location in krogerResponse.Data)
        {
            var address = location.Address;

            if (
                string.IsNullOrWhiteSpace(location.LocationId) ||
                address is null ||
                string.IsNullOrWhiteSpace(address.AddressLine1) ||
                string.IsNullOrWhiteSpace(address.City) ||
                string.IsNullOrWhiteSpace(address.State) ||
                string.IsNullOrWhiteSpace(address.ZipCode))
            {
                continue;
            }

            results.Add(
                new ProviderStoreLocation
                {
                    ExternalLocationId =
                        location.LocationId,

                    Name =
                        location.Name,

                    AddressLine1 =
                        address.AddressLine1,

                    AddressLine2 =
                        address.AddressLine2,

                    City =
                        address.City,

                    State =
                        address.State,

                    PostalCode =
                        address.ZipCode,

                    Latitude =
                        location.Geolocation?.Latitude,

                    Longitude =
                        location.Geolocation?.Longitude
                }
            );
        }

        return results;
    }



    public async Task<List<ProviderProduct>>
    SearchProductsAsync(
        string retailerName,
        string externalLocationId,
        string query)
    {
        if (!SupportsRetailer(retailerName))
        {
            return new List<ProviderProduct>();
        }

        if (
            string.IsNullOrWhiteSpace(externalLocationId) ||
            string.IsNullOrWhiteSpace(query))
        {
            return new List<ProviderProduct>();
        }

        var accessToken =
            await _tokenService.GetAccessTokenAsync();

        var encodedLocationId =
            Uri.EscapeDataString(
                externalLocationId.Trim()
            );

        var encodedQuery =
            Uri.EscapeDataString(
                query.Trim()
            );

        var requestUrl =
            $"v1/products" +
            $"?filter.term={encodedQuery}" +
            $"&filter.locationId={encodedLocationId}" +
            $"&filter.limit=25";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                requestUrl
            );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        var httpClient =
            _httpClientFactory.CreateClient("Kroger");

        using var response =
            await httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var krogerResponse =
            await response.Content
                .ReadFromJsonAsync<KrogerProductListResponse>();

        var results =
            new List<ProviderProduct>();

        if (krogerResponse?.Data is null)
        {
            return results;
        }

        foreach (var product in krogerResponse.Data)
        {
            if (
                string.IsNullOrWhiteSpace(product.ProductId) ||
                string.IsNullOrWhiteSpace(product.Description))
            {
                continue;
            }

            var size =
                product.Items?
                    .Select(item => item.Size)
                    .FirstOrDefault(
                        itemSize =>
                            !string.IsNullOrWhiteSpace(
                                itemSize
                            )
                    );

            results.Add(
                new ProviderProduct
                {
                    ExternalProductId =
                        product.ProductId,

                    Name =
                        product.Description,

                    Brand =
                        product.Brand,

                    Size =
                        size,

                    Upc =
                        product.Upc
                }
            );
        }

        return results;
    }
    public async Task<ProviderPriceSnapshot?>
    GetPriceAsync(
        string retailerName,
        string externalLocationId,
        string externalProductId)
    {
        if (!SupportsRetailer(retailerName))
        {
            return null;
        }

        if (
            string.IsNullOrWhiteSpace(externalLocationId) ||
            string.IsNullOrWhiteSpace(externalProductId))
        {
            return null;
        }

        var accessToken =
            await _tokenService.GetAccessTokenAsync();

        var encodedLocationId =
            Uri.EscapeDataString(
                externalLocationId.Trim()
            );

        var encodedProductId =
            Uri.EscapeDataString(
                externalProductId.Trim()
            );

        var requestUrl =
            $"v1/products/{encodedProductId}" +
            $"?filter.locationId={encodedLocationId}";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                requestUrl
            );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        var httpClient =
            _httpClientFactory.CreateClient("Kroger");

        using var response =
            await httpClient.SendAsync(request);

        if (
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var krogerResponse =
            await response.Content
                .ReadFromJsonAsync<KrogerProductDetailsResponse>();

        var product = krogerResponse?.Data;

        if (product is null)
        {
            return null;
        }

        var item =
            product.Items?
                .FirstOrDefault(item =>
                    (item.Price?.Regular ?? 0m) > 0m ||
                    (item.Price?.Promo ?? 0m) > 0m
                )
            ?? product.Items?.FirstOrDefault();

        if (item is null)
        {
            return new ProviderPriceSnapshot
            {
                AvailabilityStatus =
                    AvailabilityStatus.Unknown,

                SourceUpdatedAt = null
            };
        }

        return new ProviderPriceSnapshot
        {
            RegularPrice =
                NormalizePrice(item.Price?.Regular),

            SalePrice =
                NormalizePrice(item.Price?.Promo),

            MemberPrice =
                null,

            AvailabilityStatus =
                MapAvailability(
                    item.Inventory?.StockLevel
                ),

            SourceUpdatedAt =
                null
        };
    }
    private static decimal? NormalizePrice(
    decimal? price)
    {
        if (price is null || price <= 0)
        {
            return null;
        }

        return price;
    }
    private static AvailabilityStatus MapAvailability(
    string? stockLevel)
    {
        if (string.IsNullOrWhiteSpace(stockLevel))
        {
            return AvailabilityStatus.Unknown;
        }

        var normalized =
            stockLevel
                .Trim()
                .ToUpperInvariant();

        if (normalized.Contains("OUT_OF_STOCK"))
        {
            return AvailabilityStatus.Unavailable;
        }

        if (
            normalized == "HIGH" ||
            normalized == "LOW" ||
            normalized == "IN_STOCK" ||
            normalized == "AVAILABLE")
        {
            return AvailabilityStatus.Available;
        }

        return AvailabilityStatus.Unknown;
    }
    public async Task<ProviderProduct?>
    GetProductAsync(
        string retailerName,
        string externalLocationId,
        string externalProductId)
    {
        if (!SupportsRetailer(retailerName))
        {
            return null;
        }

        if (
            string.IsNullOrWhiteSpace(externalLocationId) ||
            string.IsNullOrWhiteSpace(externalProductId))
        {
            return null;
        }

        var accessToken =
            await _tokenService.GetAccessTokenAsync();

        var encodedLocationId =
            Uri.EscapeDataString(
                externalLocationId.Trim()
            );

        var encodedProductId =
            Uri.EscapeDataString(
                externalProductId.Trim()
            );

        var requestUrl =
            $"v1/products/{encodedProductId}" +
            $"?filter.locationId={encodedLocationId}";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                requestUrl
            );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        var httpClient =
            _httpClientFactory.CreateClient("Kroger");

        using var response =
            await httpClient.SendAsync(request);

        if (
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var krogerResponse =
            await response.Content
                .ReadFromJsonAsync<KrogerProductDetailsResponse>();

        var product =
            krogerResponse?.Data;

        if (
            product is null ||
            string.IsNullOrWhiteSpace(product.ProductId) ||
            string.IsNullOrWhiteSpace(product.Description))
        {
            return null;
        }

        var size =
            product.Items?
                .Select(item => item.Size)
                .FirstOrDefault(
                    itemSize =>
                        !string.IsNullOrWhiteSpace(itemSize)
                );

        return new ProviderProduct
        {
            ExternalProductId =
                product.ProductId,

            Name =
                product.Description,

            Brand =
                product.Brand,

            Size =
                size,

            Upc =
                product.Upc
        };
    }
}