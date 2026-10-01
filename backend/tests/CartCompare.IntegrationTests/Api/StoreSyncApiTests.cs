using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Providers.Models;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Api;

public class StoreSyncApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task LowesFoodsDevelopmentSync_ReusesDiscoveredStore()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Lowes Foods");

        factory.TestPriceProvider.StoresToReturn =
            new List<ProviderStoreLocation>
            {
                new()
                {
                    ExternalLocationId =
                        "4rw2mRMnac9YV56NM4GY1N",
                    Name = "LOWES 185 GARNER, NC",
                    AddressLine1 = "1845 AVERSBORO ROAD",
                    City = "GARNER",
                    State = "NC",
                    PostalCode = "27529"
                }
            };

        using var client =
            factory.CreateClient();

        const string url =
            "/api/dev/lowes-foods/sync-stores?postalCode=27529";

        using var firstResponse =
            await client.PostAsync(url, null);

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode
        );

        using var first =
            JsonDocument.Parse(
                await firstResponse.Content.ReadAsStringAsync()
            );

        Assert.Equal(
            1,
            first.RootElement
                .GetProperty("sync")
                .GetProperty("createdCount")
                .GetInt32()
        );

        var firstStore =
            first.RootElement
                .GetProperty("persistedStores")[0];

        var storeId =
            firstStore.GetProperty("id").GetInt32();

        Assert.Equal(
            storeId,
            first.RootElement
                .GetProperty("sync")
                .GetProperty("syncedStoreLocationIds")[0]
                .GetInt32()
        );

        Assert.Equal(
            "4rw2mRMnac9YV56NM4GY1N",
            firstStore
                .GetProperty("externalLocationId")
                .GetString()
        );

        factory.TestPriceProvider
            .StoresToReturn[0]
            .Name = "Lowes Foods #185 - Garner";

        using var secondResponse =
            await client.PostAsync(url, null);

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode
        );

        using var second =
            JsonDocument.Parse(
                await secondResponse.Content.ReadAsStringAsync()
            );

        var secondSync =
            second.RootElement.GetProperty("sync");

        Assert.Equal(
            0,
            secondSync.GetProperty("createdCount").GetInt32()
        );

        Assert.Equal(
            1,
            secondSync.GetProperty("updatedCount").GetInt32()
        );

        var secondStores =
            second.RootElement
                .GetProperty("persistedStores");

        Assert.Equal(1, secondStores.GetArrayLength());
        Assert.Equal(
            storeId,
            secondStores[0].GetProperty("id").GetInt32()
        );

        Assert.Equal(
            storeId,
            secondSync
                .GetProperty("syncedStoreLocationIds")[0]
                .GetInt32()
        );

        Assert.Equal(
            "Lowes Foods #185 - Garner",
            secondStores[0]
                .GetProperty("name")
                .GetString()
        );

        Assert.Equal(
            2,
            factory.TestPriceProvider.FindStoresCalls.Count
        );
    }

    [Fact]
    public async Task LowesFoodsDevelopmentSync_ReturnsNearbyStoresFromCurrentSearch()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Lowes Foods");

        factory.TestPriceProvider.StoresToReturn =
            new List<ProviderStoreLocation>
            {
                new()
                {
                    ExternalLocationId = "RALEIGH-191",
                    Name = "Lowes Foods #191",
                    AddressLine1 = "9600 Strickland Road",
                    City = "Raleigh",
                    State = "NC",
                    PostalCode = "27615"
                }
            };

        using var client = factory.CreateClient();

        using var nearbyResponse =
            await client.PostAsync(
                "/api/dev/lowes-foods/sync-stores?postalCode=27613",
                null
            );

        Assert.Equal(HttpStatusCode.OK, nearbyResponse.StatusCode);

        using var nearby = JsonDocument.Parse(
            await nearbyResponse.Content.ReadAsStringAsync()
        );

        var nearbyStore =
            nearby.RootElement
                .GetProperty("persistedStores")[0];

        Assert.Equal(
            "27615",
            nearbyStore.GetProperty("postalCode").GetString()
        );

        var nearbyStoreId =
            nearbyStore.GetProperty("id").GetInt32();

        Assert.Equal(
            nearbyStoreId,
            nearby.RootElement
                .GetProperty("sync")
                .GetProperty("syncedStoreLocationIds")[0]
                .GetInt32()
        );

        factory.TestPriceProvider.StoresToReturn =
            new List<ProviderStoreLocation>
            {
                new()
                {
                    ExternalLocationId = "GARNER-185",
                    Name = "Lowes Foods #185",
                    AddressLine1 = "1845 Aversboro Road",
                    City = "Garner",
                    State = "NC",
                    PostalCode = "27529"
                }
            };

        using var garnerResponse =
            await client.PostAsync(
                "/api/dev/lowes-foods/sync-stores?postalCode=27529",
                null
            );

        Assert.Equal(HttpStatusCode.OK, garnerResponse.StatusCode);

        using var garner = JsonDocument.Parse(
            await garnerResponse.Content.ReadAsStringAsync()
        );

        var currentStores =
            garner.RootElement.GetProperty("persistedStores");

        Assert.Equal(1, currentStores.GetArrayLength());
        Assert.Equal(
            "GARNER-185",
            currentStores[0]
                .GetProperty("externalLocationId")
                .GetString()
        );

        Assert.NotEqual(
            nearbyStoreId,
            currentStores[0].GetProperty("id").GetInt32()
        );
    }

    [Fact]
    public async Task Sync_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var request =
            new
            {
                RetailerId = 1,
                PostalCode = "27606"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/store-sync",
                request
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task Sync_WithInvalidRequest_ReturnsBadRequest()
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
                "invalid-store-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                RetailerId = 0,
                PostalCode = " "
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/store-sync",
                request
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
            "Retailer ID and postal code are required.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestPriceProvider
                .FindStoresCalls
        );
    }

    [Fact]
    public async Task Sync_WhenRetailerDoesNotExist_ReturnsNotFound()
    {
        // Arrange
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
                "missing-retailer-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                RetailerId = 999999,
                PostalCode = "27606"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/store-sync",
                request
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
            "Retailer not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestPriceProvider
                .FindStoresCalls
        );
    }

    [Fact]
    public async Task Sync_WhenProviderDoesNotSupportRetailer_ReturnsBadRequest()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Unsupported Retailer"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        // Notice that we intentionally DO NOT add
        // "Unsupported Retailer" to SupportedRetailers.

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "unsupported-provider-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                RetailerId = retailerId,
                PostalCode = "27606"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/store-sync",
                request
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
                .FindStoresCalls
        );
    }

    [Fact]
    public async Task Sync_WhenProviderReturnsStores_PersistsStoresAndReturnsCounts()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Kroger");

        factory.TestPriceProvider
            .StoresToReturn
            .AddRange(
                new[]
                {
                    new ProviderStoreLocation
                    {
                        ExternalLocationId =
                            "KROGER-101",

                        Name =
                            "Kroger Test One",

                        AddressLine1 =
                            "100 Test Street",

                        AddressLine2 =
                            null,

                        City =
                            "Raleigh",

                        State =
                            "NC",

                        PostalCode =
                            "27606",

                        Latitude =
                            35.780m,

                        Longitude =
                            -78.640m
                    },

                    new ProviderStoreLocation
                    {
                        ExternalLocationId =
                            "KROGER-102",

                        Name =
                            "Kroger Test Two",

                        AddressLine1 =
                            "200 Test Avenue",

                        AddressLine2 =
                            "Suite 1",

                        City =
                            "Raleigh",

                        State =
                            "NC",

                        PostalCode =
                            "27607",

                        Latitude =
                            35.790m,

                        Longitude =
                            -78.650m
                    }
                }
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "successful-store-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                RetailerId =
                    retailerId,

                // Deliberate spaces so we can prove that
                // StoreLocationSyncService trims it before
                // calling the provider.
                PostalCode =
                    " 27606 "
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/store-sync",
                request
            );

        // Assert: HTTP result
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
            retailerId,
            root.GetProperty("retailerId")
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
            2,
            root.GetProperty("providerStoreCount")
                .GetInt32()
        );

        Assert.Equal(
            2,
            root.GetProperty("createdCount")
                .GetInt32()
        );

        Assert.Equal(
            0,
            root.GetProperty("updatedCount")
                .GetInt32()
        );

        // Assert: provider interaction
        Assert.Single(
            factory.TestPriceProvider
                .FindStoresCalls
        );

        var providerCall =
            factory.TestPriceProvider
                .FindStoresCalls
                .Single();

        Assert.Equal(
            "Kroger",
            providerCall.RetailerName
        );

        Assert.Equal(
            "27606",
            providerCall.PostalCode
        );

        // Assert: actual PostgreSQL persistence
        await using var context =
            CreateDbContext();

        var stores =
            await context
                .Set<StoreLocation>()
                .OrderBy(
                    store =>
                        store.ExternalLocationId
                )
                .ToListAsync();

        Assert.Equal(
            2,
            stores.Count
        );

        var firstStore =
            stores[0];

        Assert.Equal(
            retailerId,
            firstStore.RetailerId
        );

        Assert.Equal(
            "KROGER-101",
            firstStore.ExternalLocationId
        );

        Assert.Equal(
            "Kroger Test One",
            firstStore.Name
        );

        Assert.Equal(
            "100 Test Street",
            firstStore.AddressLine1
        );

        Assert.Equal(
            "Raleigh",
            firstStore.City
        );

        Assert.Equal(
            "NC",
            firstStore.State
        );

        Assert.Equal(
            "27606",
            firstStore.PostalCode
        );

        Assert.Equal(
            35.780m,
            firstStore.Latitude
        );

        Assert.Equal(
            -78.640m,
            firstStore.Longitude
        );

        Assert.True(
            firstStore.IsActive
        );
    }

    [Fact]
    public async Task Sync_WhenStoreAlreadyExists_UpdatesExistingRowInsteadOfCreatingDuplicate()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Kroger");

        factory.TestPriceProvider
            .StoresToReturn
            .Add(
                new ProviderStoreLocation
                {
                    ExternalLocationId =
                        "KROGER-101",

                    Name =
                        "Old Store Name",

                    AddressLine1 =
                        "100 Old Street",

                    AddressLine2 =
                        null,

                    City =
                        "Raleigh",

                    State =
                        "NC",

                    PostalCode =
                        "27606",

                    Latitude =
                        35.780m,

                    Longitude =
                        -78.640m
                }
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "repeat-store-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                RetailerId =
                    retailerId,

                PostalCode =
                    "27606"
            };

        // First sync should create the store.
        var firstResponse =
            await client.PostAsJsonAsync(
                "/api/store-sync",
                request
            );

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode
        );

        // Change what the provider reports for the
        // exact same external store ID.
        factory.TestPriceProvider
            .StoresToReturn
            .Clear();

        factory.TestPriceProvider
            .StoresToReturn
            .Add(
                new ProviderStoreLocation
                {
                    ExternalLocationId =
                        "KROGER-101",

                    Name =
                        "Updated Store Name",

                    AddressLine1 =
                        "999 Updated Street",

                    AddressLine2 =
                        "Suite 5",

                    City =
                        "Cary",

                    State =
                        "NC",

                    PostalCode =
                        "27511",

                    Latitude =
                        35.790m,

                    Longitude =
                        -78.780m
                }
            );

        // Act: sync again.
        var secondResponse =
            await client.PostAsJsonAsync(
                "/api/store-sync",
                request
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode
        );

        var json =
            await secondResponse.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        var root =
            document.RootElement;

        Assert.Equal(
            1,
            root.GetProperty("providerStoreCount")
                .GetInt32()
        );

        Assert.Equal(
            0,
            root.GetProperty("createdCount")
                .GetInt32()
        );

        Assert.Equal(
            1,
            root.GetProperty("updatedCount")
                .GetInt32()
        );

        // Provider was called once for each sync.
        Assert.Equal(
            2,
            factory.TestPriceProvider
                .FindStoresCalls
                .Count
        );

        // Most importantly, we still have ONE
        // StoreLocation row rather than a duplicate.
        await using var context =
            CreateDbContext();

        var stores =
            await context
                .Set<StoreLocation>()
                .Where(
                    store =>
                        store.RetailerId ==
                            retailerId
                        &&
                        store.ExternalLocationId ==
                            "KROGER-101"
                )
                .ToListAsync();

        Assert.Single(
            stores
        );

        var stored =
            stores[0];

        Assert.Equal(
            "Updated Store Name",
            stored.Name
        );

        Assert.Equal(
            "999 Updated Street",
            stored.AddressLine1
        );

        Assert.Equal(
            "Suite 5",
            stored.AddressLine2
        );

        Assert.Equal(
            "Cary",
            stored.City
        );

        Assert.Equal(
            "27511",
            stored.PostalCode
        );

        Assert.Equal(
            35.790m,
            stored.Latitude
        );

        Assert.Equal(
            -78.780m,
            stored.Longitude
        );

        Assert.True(
            stored.IsActive
        );
    }

    // =================================================
    // DATABASE HELPERS
    // =================================================

    private async Task<int> SeedRetailerAsync(
        string name)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            new Retailer
            {
                Name =
                    name,

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

        return retailer.Id;
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
}
