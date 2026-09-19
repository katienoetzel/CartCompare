using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Providers.Models;

namespace CartCompare.IntegrationTests.Api;

public class ProductCandidatesApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task GetCandidates_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        // Act
        var response =
            await client.GetAsync(
                "/api/product-candidates"
                + "?itemId=1"
                + "&storeLocationId=1"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task GetCandidates_WithInvalidIds_ReturnsBadRequest()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "invalid-candidates@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/product-candidates"
                + "?itemId=0"
                + "&storeLocationId=-1"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "Item ID and store location ID must be greater than zero.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestPriceProvider
                .SearchProductsCalls
        );
    }

    [Fact]
    public async Task GetCandidates_WhenItemDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var seeded =
            await SeedRetailerAndStoreAsync(
                "Kroger"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Kroger");

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "missing-item-candidates@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/product-candidates"
                + "?itemId=999999"
                + $"&storeLocationId={seeded.StoreId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "Item not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestPriceProvider
                .SearchProductsCalls
        );
    }

    [Fact]
    public async Task GetCandidates_WhenStoreDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var itemId =
            await SeedItemAsync(
                "Whole Milk"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Kroger");

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "missing-store-candidates@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/product-candidates"
                + $"?itemId={itemId}"
                + "&storeLocationId=999999"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "Store location not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestPriceProvider
                .SearchProductsCalls
        );
    }

    [Fact]
    public async Task GetCandidates_WhenProviderDoesNotSupportRetailer_ReturnsBadRequest()
    {
        // Arrange
        var seeded =
            await SeedCandidateEnvironmentAsync(
                retailerName:
                    "Unsupported Retailer",

                itemName:
                    "Whole Milk"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        // Deliberately do not add the retailer
        // to SupportedRetailers.

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "unsupported-candidates@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/product-candidates"
                + $"?itemId={seeded.ItemId}"
                + $"&storeLocationId={seeded.StoreId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "No price provider supports this retailer.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestPriceProvider
                .SearchProductsCalls
        );
    }

    [Fact]
    public async Task GetCandidates_WithExplicitQuery_UsesTrimmedQueryAndReturnsProviderProducts()
    {
        // Arrange
        var seeded =
            await SeedCandidateEnvironmentAsync(
                retailerName:
                    "Kroger",

                itemName:
                    "Whole Milk"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Kroger");

        factory.TestPriceProvider
            .ProductsToReturn
            .AddRange(
                new[]
                {
                    new ProviderProduct
                    {
                        ExternalProductId =
                            "MILK-001",

                        Name =
                            "Whole Milk",

                        Brand =
                            "Test Brand",

                        Size =
                            "1 gallon",

                        Upc =
                            "012345678905"
                    },

                    new ProviderProduct
                    {
                        ExternalProductId =
                            "MILK-002",

                        Name =
                            "Chocolate Milk",

                        Brand =
                            "Other Brand",

                        Size =
                            "1 quart",

                        Upc =
                            "987654321098"
                    }
                }
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "successful-candidates@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // %20 encodes spaces around the search text.
        // ProductCandidateService should trim those
        // spaces before calling the provider.
        var response =
            await client.GetAsync(
                "/api/product-candidates"
                + $"?itemId={seeded.ItemId}"
                + $"&storeLocationId={seeded.StoreId}"
                + "&query=%20milk%20"
            );

        // Assert: HTTP response
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        var root =
            document.RootElement;

        Assert.Equal(
            seeded.ItemId,
            root.GetProperty("itemId")
                .GetInt32()
        );

        Assert.Equal(
            seeded.StoreId,
            root.GetProperty("storeLocationId")
                .GetInt32()
        );

        Assert.Equal(
            "Kroger",
            root.GetProperty("retailerName")
                .GetString()
        );

        Assert.Equal(
            "FakePriceProvider",
            root.GetProperty("providerName")
                .GetString()
        );

        Assert.Equal(
            "milk",
            root.GetProperty("searchQuery")
                .GetString()
        );

        var candidates =
            root.GetProperty("candidates");

        Assert.Equal(
            2,
            candidates.GetArrayLength()
        );

        var firstProviderProduct =
            FindCandidate(
                candidates,
                "MILK-001"
            );

        Assert.Equal(
            "Whole Milk",
            firstProviderProduct
                .GetProperty("name")
                .GetString()
        );

        Assert.Equal(
            "Test Brand",
            firstProviderProduct
                .GetProperty("brand")
                .GetString()
        );

        Assert.Equal(
            "1 gallon",
            firstProviderProduct
                .GetProperty("size")
                .GetString()
        );

        Assert.Equal(
            "012345678905",
            firstProviderProduct
                .GetProperty("upc")
                .GetString()
        );

        // These values are produced by the REAL
        // ProductMatchingService. We don't duplicate
        // all its unit tests here; we only verify that
        // matching metadata made it through the API.
        Assert.Equal(
            JsonValueKind.True,
            firstProviderProduct
                .GetProperty("isMatch")
                .ValueKind
        );

        Assert.False(
            string.IsNullOrWhiteSpace(
                firstProviderProduct
                    .GetProperty("matchReason")
                    .GetString()
            )
        );

        // Assert: actual provider invocation
        Assert.Single(
            factory.TestPriceProvider
                .SearchProductsCalls
        );

        var providerCall =
            factory.TestPriceProvider
                .SearchProductsCalls
                .Single();

        Assert.Equal(
            "Kroger",
            providerCall.RetailerName
        );

        Assert.Equal(
            "STORE-101",
            providerCall.ExternalLocationId
        );

        Assert.Equal(
            "milk",
            providerCall.Query
        );
    }

    [Fact]
    public async Task GetCandidates_WithoutQuery_UsesTrimmedItemName()
    {
        // Arrange
        var seeded =
            await SeedCandidateEnvironmentAsync(
                retailerName:
                    "Kroger",

                itemName:
                    "  Whole Milk  "
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Kroger");

        factory.TestPriceProvider
            .ProductsToReturn
            .Add(
                new ProviderProduct
                {
                    ExternalProductId =
                        "MILK-001",

                    Name =
                        "Whole Milk",

                    Brand =
                        "Test Brand",

                    Size =
                        "1 gallon",

                    Upc =
                        null
                }
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "default-query-candidates@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/product-candidates"
                + $"?itemId={seeded.ItemId}"
                + $"&storeLocationId={seeded.StoreId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "Whole Milk",
            document.RootElement
                .GetProperty("searchQuery")
                .GetString()
        );

        Assert.Single(
            factory.TestPriceProvider
                .SearchProductsCalls
        );

        var providerCall =
            factory.TestPriceProvider
                .SearchProductsCalls
                .Single();

        Assert.Equal(
            "Whole Milk",
            providerCall.Query
        );
    }

    // =================================================
    // DATABASE HELPERS
    // =================================================

    private async Task<(
        int RetailerId,
        int StoreId)>
        SeedRetailerAndStoreAsync(
            string retailerName)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            new Retailer
            {
                Name =
                    retailerName,

                SupportsMembership =
                    true,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        var store =
            new StoreLocation
            {
                RetailerId =
                    retailer.Id,

                ExternalLocationId =
                    "STORE-101",

                Name =
                    "Test Store",

                AddressLine1 =
                    "123 Test Street",

                City =
                    "Raleigh",

                State =
                    "NC",

                PostalCode =
                    "27606",

                IsActive =
                    true,

                LastSeenAt =
                    DateTime.UtcNow
            };

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return (
            retailer.Id,
            store.Id
        );
    }

    private async Task<int> SeedItemAsync(
        string itemName)
    {
        await using var context =
            CreateDbContext();

        var now =
            DateTime.UtcNow;

        var item =
            new Item
            {
                Name =
                    itemName,

                Brand =
                    "Test Brand",

                Size =
                    "1 gallon",

                Category =
                    "Dairy",

                IsActive =
                    true,

                CreatedAt =
                    now,

                UpdatedAt =
                    now
            };

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return item.Id;
    }

    private async Task<(
        int RetailerId,
        int StoreId,
        int ItemId)>
        SeedCandidateEnvironmentAsync(
            string retailerName,
            string itemName)
    {
        await using var context =
            CreateDbContext();

        var now =
            DateTime.UtcNow;

        var retailer =
            new Retailer
            {
                Name =
                    retailerName,

                SupportsMembership =
                    true,

                IsActive =
                    true,

                CreatedAt =
                    now
            };

        var item =
            new Item
            {
                Name =
                    itemName,

                Brand =
                    "Test Brand",

                Size =
                    "1 gallon",

                Category =
                    "Dairy",

                IsActive =
                    true,

                CreatedAt =
                    now,

                UpdatedAt =
                    now
            };

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var store =
            new StoreLocation
            {
                RetailerId =
                    retailer.Id,

                ExternalLocationId =
                    "STORE-101",

                Name =
                    "Test Store",

                AddressLine1 =
                    "123 Test Street",

                City =
                    "Raleigh",

                State =
                    "NC",

                PostalCode =
                    "27606",

                IsActive =
                    true,

                LastSeenAt =
                    now
            };

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return (
            retailer.Id,
            store.Id,
            item.Id
        );
    }

    // =================================================
    // AUTHENTICATION HELPERS
    // =================================================

    private static async Task<string>
        RegisterAndGetTokenAsync(
            HttpClient client,
            string email)
    {
        var request =
            new
            {
                FirstName =
                    "Katie",

                LastName =
                    "Tester",

                Email =
                    email,

                Password =
                    "CartCompare123!",

                DefaultPostalCode =
                    "27606"
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                request
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        var token =
            document.RootElement
                .GetProperty("token")
                .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(
                token
            )
        );

        return token!;
    }

    private static void SetBearerToken(
        HttpClient client,
        string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token
            );
    }

    // =================================================
    // JSON HELPERS
    // =================================================

    private static JsonElement FindCandidate(
        JsonElement candidates,
        string externalProductId)
    {
        foreach (
            var candidate
            in candidates.EnumerateArray()
        )
        {
            if (
                candidate
                    .GetProperty(
                        "externalProductId"
                    )
                    .GetString()
                ==
                externalProductId
            )
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Candidate "
            + externalProductId
            + " was not present in the response."
        );
    }
}