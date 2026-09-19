using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Api;

public class CatalogApiTests
    : PostgresIntegrationTestBase
{
    // =================================================
    // ITEMS
    // =================================================

    [Fact]
    public async Task Items_GetActive_ReturnsOnlyActiveItems()
    {
        // Arrange
        var activeItemId =
            await SeedItemAsync(
                "Whole Milk",
                isActive: true
            );

        var inactiveItemId =
            await SeedItemAsync(
                "Discontinued Milk",
                isActive: false
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        // Act
        var response =
            await client.GetAsync(
                "/api/items"
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
            1,
            document.RootElement
                .GetArrayLength()
        );

        var item =
            document.RootElement[0];

        Assert.Equal(
            activeItemId,
            item.GetProperty("id")
                .GetInt32()
        );

        Assert.NotEqual(
            inactiveItemId,
            item.GetProperty("id")
                .GetInt32()
        );
    }

    [Fact]
    public async Task Items_GetById_WhenItemExists_ReturnsItem()
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

        using var client =
            factory.CreateClient();

        // Act
        var response =
            await client.GetAsync(
                $"/api/items/{itemId}"
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
            itemId,
            document.RootElement
                .GetProperty("id")
                .GetInt32()
        );

        Assert.Equal(
            "Whole Milk",
            document.RootElement
                .GetProperty("name")
                .GetString()
        );
    }

    [Fact]
    public async Task Items_GetById_WhenItemDoesNotExist_ReturnsNotFound()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                "/api/items/999999"
            );

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );
    }

    [Fact]
    public async Task Items_Create_WithoutToken_ReturnsUnauthorized()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var request =
            new
            {
                Name =
                    "Whole Milk",

                Brand =
                    "Test Brand",

                Size =
                    "1 gallon",

                Category =
                    "Dairy"
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/items",
                request
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task Items_Create_WithToken_PersistsItem()
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
                "create-item-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                Name =
                    "Whole Milk",

                Brand =
                    "Test Brand",

                Size =
                    "1 gallon",

                Category =
                    "Dairy"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/items",
                request
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        var item =
            await context
                .Set<Item>()
                .SingleAsync();

        Assert.Equal(
            "Whole Milk",
            item.Name
        );

        Assert.Equal(
            "Test Brand",
            item.Brand
        );

        Assert.True(
            item.IsActive
        );
    }

    // =================================================
    // RETAILERS
    // =================================================

    [Fact]
    public async Task Retailers_GetActive_ReturnsOnlyActiveRetailers()
    {
        // Arrange
        var activeRetailerId =
            await SeedRetailerAsync(
                "Kroger",
                supportsMembership: true,
                isActive: true
            );

        var inactiveRetailerId =
            await SeedRetailerAsync(
                "Inactive Retailer",
                supportsMembership: false,
                isActive: false
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        // Act
        var response =
            await client.GetAsync(
                "/api/retailers"
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
            1,
            document.RootElement
                .GetArrayLength()
        );

        Assert.Equal(
            activeRetailerId,
            document.RootElement[0]
                .GetProperty("id")
                .GetInt32()
        );

        Assert.NotEqual(
            inactiveRetailerId,
            document.RootElement[0]
                .GetProperty("id")
                .GetInt32()
        );
    }

    [Fact]
    public async Task Retailers_Create_WithoutToken_ReturnsUnauthorized()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var request =
            new
            {
                Name =
                    "Kroger",

                SupportsMembership =
                    true
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/retailers",
                request
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        Assert.Equal(
            0,
            await context
                .Set<Retailer>()
                .CountAsync()
        );
    }

    [Fact]
    public async Task Retailers_Create_WithToken_PersistsRetailer()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "create-retailer-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                Name =
                    "Kroger",

                SupportsMembership =
                    true
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/retailers",
                request
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        var retailer =
            await context
                .Set<Retailer>()
                .SingleAsync();

        Assert.Equal(
            "Kroger",
            retailer.Name
        );

        Assert.True(
            retailer.SupportsMembership
        );

        Assert.True(
            retailer.IsActive
        );
    }

    // =================================================
    // STORE LOCATIONS
    // =================================================

    [Fact]
    public async Task StoreLocations_GetByRetailer_ReturnsOnlyRequestedRetailersStores()
    {
        // Arrange
        var firstRetailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        var secondRetailerId =
            await SeedRetailerAsync(
                "Other Retailer"
            );

        var firstStoreId =
            await SeedStoreAsync(
                firstRetailerId,
                "STORE-1",
                "Kroger One"
            );

        await SeedStoreAsync(
            secondRetailerId,
            "STORE-2",
            "Other Store"
        );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        // Act
        var response =
            await client.GetAsync(
                $"/api/store-locations/retailer/{firstRetailerId}"
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
            1,
            document.RootElement
                .GetArrayLength()
        );

        Assert.Equal(
            firstStoreId,
            document.RootElement[0]
                .GetProperty("id")
                .GetInt32()
        );

        Assert.Equal(
            firstRetailerId,
            document.RootElement[0]
                .GetProperty("retailerId")
                .GetInt32()
        );
    }

    [Fact]
    public async Task StoreLocations_GetById_WhenStoreDoesNotExist_ReturnsNotFound()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                "/api/store-locations/999999"
            );

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );
    }

    [Fact]
    public async Task StoreLocations_Create_WithoutToken_ReturnsUnauthorized()
    {
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var request =
            CreateStoreRequest(
                retailerId,
                "STORE-1"
            );

        var response =
            await client.PostAsJsonAsync(
                "/api/store-locations",
                request
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task StoreLocations_Create_WithToken_PersistsStore()
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

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "create-store-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateStoreRequest(
                retailerId,
                "STORE-1"
            );

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/store-locations",
                request
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        var store =
            await context
                .Set<StoreLocation>()
                .SingleAsync();

        Assert.Equal(
            retailerId,
            store.RetailerId
        );

        Assert.Equal(
            "STORE-1",
            store.ExternalLocationId
        );
    }

    [Fact]
    public async Task StoreLocations_Create_WhenRetailerDoesNotExist_ReturnsNotFound()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "missing-retailer-store@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateStoreRequest(
                retailerId: 999999,
                externalLocationId:
                    "STORE-1"
            );

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/store-locations",
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
    }

    // =================================================
    // RETAILER PRODUCTS
    // =================================================

    [Fact]
    public async Task RetailerProducts_GetById_WhenProductDoesNotExist_ReturnsNotFound()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                "/api/retailer-products/999999"
            );

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );
    }

    [Fact]
    public async Task RetailerProducts_GetByRetailer_ReturnsOnlyRequestedRetailersProducts()
    {
        // Arrange
        var firstRetailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        var secondRetailerId =
            await SeedRetailerAsync(
                "Other Retailer"
            );

        var itemId =
            await SeedItemAsync(
                "Whole Milk"
            );

        var firstProductId =
            await SeedProductAsync(
                itemId,
                firstRetailerId,
                "PRODUCT-1"
            );

        await SeedProductAsync(
            itemId,
            secondRetailerId,
            "PRODUCT-2"
        );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        // Act
        var response =
            await client.GetAsync(
                $"/api/retailer-products/retailer/{firstRetailerId}"
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
            1,
            document.RootElement
                .GetArrayLength()
        );

        Assert.Equal(
            firstProductId,
            document.RootElement[0]
                .GetProperty("id")
                .GetInt32()
        );

        Assert.Equal(
            firstRetailerId,
            document.RootElement[0]
                .GetProperty("retailerId")
                .GetInt32()
        );
    }

    [Fact]
    public async Task RetailerProducts_GetByItem_ReturnsOnlyRequestedItemsProducts()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        var firstItemId =
            await SeedItemAsync(
                "Whole Milk"
            );

        var secondItemId =
            await SeedItemAsync(
                "Large Eggs"
            );

        var firstProductId =
            await SeedProductAsync(
                firstItemId,
                retailerId,
                "PRODUCT-1"
            );

        await SeedProductAsync(
            secondItemId,
            retailerId,
            "PRODUCT-2"
        );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        // Act
        var response =
            await client.GetAsync(
                $"/api/retailer-products/item/{firstItemId}"
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
            1,
            document.RootElement
                .GetArrayLength()
        );

        Assert.Equal(
            firstProductId,
            document.RootElement[0]
                .GetProperty("id")
                .GetInt32()
        );

        Assert.Equal(
            firstItemId,
            document.RootElement[0]
                .GetProperty("itemId")
                .GetInt32()
        );
    }

    [Fact]
    public async Task RetailerProducts_Create_WithoutToken_ReturnsUnauthorized()
    {
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        var itemId =
            await SeedItemAsync(
                "Whole Milk"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var request =
            CreateProductRequest(
                itemId,
                retailerId,
                "PRODUCT-1"
            );

        var response =
            await client.PostAsJsonAsync(
                "/api/retailer-products",
                request
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task RetailerProducts_Create_WithToken_PersistsProduct()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        var itemId =
            await SeedItemAsync(
                "Whole Milk"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "create-product-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateProductRequest(
                itemId,
                retailerId,
                "PRODUCT-1"
            );

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/retailer-products",
                request
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        var product =
            await context
                .Set<RetailerProduct>()
                .SingleAsync();

        Assert.Equal(
            itemId,
            product.ItemId
        );

        Assert.Equal(
            retailerId,
            product.RetailerId
        );

        Assert.Equal(
            "PRODUCT-1",
            product.ExternalProductId
        );
    }

    [Fact]
    public async Task RetailerProducts_Create_WhenRetailerDoesNotExist_ReturnsNotFound()
    {
        var itemId =
            await SeedItemAsync(
                "Whole Milk"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "missing-product-retailer@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateProductRequest(
                itemId,
                retailerId: 999999,
                externalProductId:
                    "PRODUCT-1"
            );

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/retailer-products",
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
    }

    [Fact]
    public async Task RetailerProducts_Create_WhenItemDoesNotExist_ReturnsNotFound()
    {
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "missing-product-item@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateProductRequest(
                itemId: 999999,
                retailerId: retailerId,
                externalProductId:
                    "PRODUCT-1"
            );

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/retailer-products",
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
            "Item not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );
    }

    // =================================================
    // REQUEST HELPERS
    // =================================================

    private static object CreateStoreRequest(
        int retailerId,
        string externalLocationId)
    {
        return new
        {
            RetailerId =
                retailerId,

            ExternalLocationId =
                externalLocationId,

            Name =
                "Test Store",

            AddressLine1 =
                "123 Test Street",

            AddressLine2 =
                (string?)null,

            City =
                "Raleigh",

            State =
                "NC",

            PostalCode =
                "27606",

            Latitude =
                35.78m,

            Longitude =
                -78.64m
        };
    }

    private static object CreateProductRequest(
        int itemId,
        int retailerId,
        string externalProductId)
    {
        return new
        {
            ItemId =
                itemId,

            RetailerId =
                retailerId,

            ExternalProductId =
                externalProductId,

            Name =
                "Test Milk",

            Brand =
                "Test Brand",

            Size =
                "1 gallon",

            Upc =
                "012345678905"
        };
    }

    // =================================================
    // DATABASE SEED HELPERS
    // =================================================

    private async Task<int> SeedRetailerAsync(
        string name,
        bool supportsMembership = true,
        bool isActive = true)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            new Retailer
            {
                Name =
                    name,

                SupportsMembership =
                    supportsMembership,

                IsActive =
                    isActive,

                CreatedAt =
                    DateTime.UtcNow
            };

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        return retailer.Id;
    }

    private async Task<int> SeedItemAsync(
        string name,
        bool isActive = true)
    {
        await using var context =
            CreateDbContext();

        var now =
            DateTime.UtcNow;

        var item =
            new Item
            {
                Name =
                    name,

                Brand =
                    "Test Brand",

                Size =
                    "Test Size",

                Category =
                    "Test Category",

                IsActive =
                    isActive,

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

    private async Task<int> SeedStoreAsync(
        int retailerId,
        string externalLocationId,
        string name)
    {
        await using var context =
            CreateDbContext();

        var store =
            new StoreLocation
            {
                RetailerId =
                    retailerId,

                ExternalLocationId =
                    externalLocationId,

                Name =
                    name,

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

        return store.Id;
    }

    private async Task<int> SeedProductAsync(
        int itemId,
        int retailerId,
        string externalProductId)
    {
        await using var context =
            CreateDbContext();

        var product =
            new RetailerProduct
            {
                ItemId =
                    itemId,

                RetailerId =
                    retailerId,

                ExternalProductId =
                    externalProductId,

                Name =
                    "Test Product",

                Brand =
                    "Test Brand",

                Size =
                    "Test Size",

                Upc =
                    null,

                MatchMethod =
                    ProductMatchMethod.Manual,

                MatchConfidence =
                    null,

                IsActive =
                    true,

                LastSeenAt =
                    DateTime.UtcNow
            };

        context.Set<RetailerProduct>()
            .Add(product);

        await context.SaveChangesAsync();

        return product.Id;
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