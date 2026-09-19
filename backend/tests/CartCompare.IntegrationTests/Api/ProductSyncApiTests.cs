using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Providers.Models;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Api;

public class ProductSyncApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task Sync_WithoutToken_ReturnsUnauthorized()
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
                ItemId = 1,
                StoreLocationId = 1,
                ExternalProductId = "PRODUCT-1"
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task Sync_WithInvalidRequest_ReturnsBadRequest()
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
                "invalid-product-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId = 0,
                StoreLocationId = 0,
                ExternalProductId = " "
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

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
            "Item ID, store location ID, and external product ID are required.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestPriceProvider
                .GetProductCalls
        );

        Assert.Empty(
            factory.TestPriceProvider
                .GetPriceCalls
        );
    }

    [Fact]
    public async Task Sync_WhenItemDoesNotExist_ReturnsNotFound()
    {
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
                "missing-item-product-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId = 999999,
                StoreLocationId = seeded.StoreId,
                ExternalProductId = "PRODUCT-1"
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

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
                .GetProductCalls
        );
    }

    [Fact]
    public async Task Sync_WhenStoreDoesNotExist_ReturnsNotFound()
    {
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
                "missing-store-product-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId = itemId,
                StoreLocationId = 999999,
                ExternalProductId = "PRODUCT-1"
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

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
                .GetProductCalls
        );
    }

    [Fact]
    public async Task Sync_WhenProviderDoesNotSupportRetailer_ReturnsBadRequest()
    {
        var seeded =
            await SeedEnvironmentAsync(
                retailerName:
                    "Unsupported Retailer",

                itemName:
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
                "unsupported-product-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId = seeded.ItemId,
                StoreLocationId = seeded.StoreId,
                ExternalProductId = "PRODUCT-1"
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

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
            "No provider supports this retailer.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestPriceProvider
                .GetProductCalls
        );
    }

    [Fact]
    public async Task Sync_WhenProviderProductDoesNotExist_ReturnsNotFound()
    {
        var seeded =
            await SeedEnvironmentAsync(
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

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "missing-provider-product@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId =
                    seeded.ItemId,

                StoreLocationId =
                    seeded.StoreId,

                ExternalProductId =
                    " PRODUCT-404 "
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

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
            "The provider product was not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Single(
            factory.TestPriceProvider
                .GetProductCalls
        );

        var providerCall =
            factory.TestPriceProvider
                .GetProductCalls
                .Single();

        Assert.Equal(
            "Kroger",
            providerCall.RetailerName
        );

        Assert.Equal(
            "STORE-101",
            providerCall.ExternalLocationId
        );

        // ProductPriceSyncService trims the request ID.
        Assert.Equal(
            "PRODUCT-404",
            providerCall.ExternalProductId
        );

        Assert.Empty(
            factory.TestPriceProvider
                .GetPriceCalls
        );
    }

    [Fact]
    public async Task Sync_WithProviderProductAndPrice_CreatesProductAndPrice()
    {
        var seeded =
            await SeedEnvironmentAsync(
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
            .ProductsByExternalId[
                "PRODUCT-1"
            ] =
                new ProviderProduct
                {
                    ExternalProductId =
                        "PRODUCT-1",

                    Name =
                        "Whole Milk",

                    Brand =
                        "Test Brand",

                    Size =
                        "1 gallon",

                    Upc =
                        "012345678905"
                };

        var sourceUpdatedAt =
            DateTime.UtcNow.AddMinutes(-5);

        factory.TestPriceProvider
            .PricesByExternalProductId[
                "PRODUCT-1"
            ] =
                new ProviderPriceSnapshot
                {
                    RegularPrice =
                        5.00m,

                    SalePrice =
                        4.00m,

                    MemberPrice =
                        3.50m,

                    AvailabilityStatus =
                        AvailabilityStatus.Available,

                    SourceUpdatedAt =
                        sourceUpdatedAt
                };

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "successful-product-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId =
                    seeded.ItemId,

                StoreLocationId =
                    seeded.StoreId,

                ExternalProductId =
                    "PRODUCT-1"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
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

        Assert.True(
            root.GetProperty("productCreated")
                .GetBoolean()
        );

        Assert.False(
            root.GetProperty("productUpdated")
                .GetBoolean()
        );

        Assert.True(
            root.GetProperty("priceSynced")
                .GetBoolean()
        );

        Assert.Equal(
            "FakePriceProvider",
            root.GetProperty("providerName")
                .GetString()
        );

        Assert.NotEqual(
            JsonValueKind.Null,
            root.GetProperty("retailerProduct")
                .ValueKind
        );

        Assert.NotEqual(
            JsonValueKind.Null,
            root.GetProperty("price")
                .ValueKind
        );

        // Assert: provider calls
        Assert.Single(
            factory.TestPriceProvider
                .GetProductCalls
        );

        Assert.Single(
            factory.TestPriceProvider
                .GetPriceCalls
        );

        var productCall =
            factory.TestPriceProvider
                .GetProductCalls
                .Single();

        Assert.Equal(
            "Kroger",
            productCall.RetailerName
        );

        Assert.Equal(
            "STORE-101",
            productCall.ExternalLocationId
        );

        Assert.Equal(
            "PRODUCT-1",
            productCall.ExternalProductId
        );

        var priceCall =
            factory.TestPriceProvider
                .GetPriceCalls
                .Single();

        Assert.Equal(
            "Kroger",
            priceCall.RetailerName
        );

        Assert.Equal(
            "STORE-101",
            priceCall.ExternalLocationId
        );

        Assert.Equal(
            "PRODUCT-1",
            priceCall.ExternalProductId
        );

        // Assert: PostgreSQL
        await using var context =
            CreateDbContext();

        var products =
            await context
                .Set<RetailerProduct>()
                .ToListAsync();

        Assert.Single(
            products
        );

        var storedProduct =
            products[0];

        Assert.Equal(
            seeded.ItemId,
            storedProduct.ItemId
        );

        Assert.Equal(
            seeded.RetailerId,
            storedProduct.RetailerId
        );

        Assert.Equal(
            "PRODUCT-1",
            storedProduct.ExternalProductId
        );

        Assert.Equal(
            "Whole Milk",
            storedProduct.Name
        );

        Assert.Equal(
            "Test Brand",
            storedProduct.Brand
        );

        Assert.Equal(
            "1 gallon",
            storedProduct.Size
        );

        Assert.Equal(
            "012345678905",
            storedProduct.Upc
        );

        Assert.True(
            storedProduct.IsActive
        );

        var prices =
            await context
                .Set<Price>()
                .ToListAsync();

        Assert.Single(
            prices
        );

        var storedPrice =
            prices[0];

        Assert.Equal(
            storedProduct.Id,
            storedPrice.RetailerProductId
        );

        Assert.Equal(
            seeded.StoreId,
            storedPrice.StoreLocationId
        );

        Assert.Equal(
            5.00m,
            storedPrice.RegularPrice
        );

        Assert.Equal(
            4.00m,
            storedPrice.SalePrice
        );

        Assert.Equal(
            3.50m,
            storedPrice.MemberPrice
        );

        Assert.Equal(
            AvailabilityStatus.Available,
            storedPrice.AvailabilityStatus
        );

        Assert.Equal(
            "FakePriceProvider",
            storedPrice.SourceProvider
        );
    }

    [Fact]
    public async Task Sync_WhenPriceIsNotAvailable_ReturnsOkAndKeepsProductWithoutPrice()
    {
        var seeded =
            await SeedEnvironmentAsync(
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
            .ProductsByExternalId[
                "PRODUCT-1"
            ] =
                new ProviderProduct
                {
                    ExternalProductId =
                        "PRODUCT-1",

                    Name =
                        "Whole Milk",

                    Brand =
                        "Test Brand",

                    Size =
                        "1 gallon",

                    Upc =
                        null
                };

        // Intentionally DO NOT add a price snapshot.

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "unavailable-price-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId =
                    seeded.ItemId,

                StoreLocationId =
                    seeded.StoreId,

                ExternalProductId =
                    "PRODUCT-1"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

        // Controller intentionally maps
        // PriceNotAvailable to HTTP 200.
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

        Assert.True(
            root.GetProperty("productCreated")
                .GetBoolean()
        );

        Assert.False(
            root.GetProperty("productUpdated")
                .GetBoolean()
        );

        Assert.False(
            root.GetProperty("priceSynced")
                .GetBoolean()
        );

        Assert.Equal(
            "FakePriceProvider",
            root.GetProperty("providerName")
                .GetString()
        );

        Assert.Single(
            factory.TestPriceProvider
                .GetPriceCalls
        );

        await using var context =
            CreateDbContext();

        Assert.Equal(
            1,
            await context
                .Set<RetailerProduct>()
                .CountAsync()
        );

        Assert.Equal(
            0,
            await context
                .Set<Price>()
                .CountAsync()
        );
    }

    [Fact]
    public async Task Sync_WhenExistingProductBelongsToDifferentItem_ReturnsConflict()
    {
        var seeded =
            await SeedEnvironmentAsync(
                retailerName:
                    "Kroger",

                itemName:
                    "Whole Milk"
            );

        var otherItemId =
            await SeedItemAsync(
                "Chocolate Milk"
            );

        await SeedRetailerProductAsync(
            itemId:
                otherItemId,

            retailerId:
                seeded.RetailerId,

            externalProductId:
                "PRODUCT-1"
        );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestPriceProvider
            .SupportedRetailers
            .Add("Kroger");

        factory.TestPriceProvider
            .ProductsByExternalId[
                "PRODUCT-1"
            ] =
                new ProviderProduct
                {
                    ExternalProductId =
                        "PRODUCT-1",

                    Name =
                        "Whole Milk",

                    Brand =
                        "Test Brand",

                    Size =
                        "1 gallon",

                    Upc =
                        null
                };

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "conflict-product-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId =
                    seeded.ItemId,

                StoreLocationId =
                    seeded.StoreId,

                ExternalProductId =
                    "PRODUCT-1"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "This retailer product is already matched to a different canonical item.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        // Conflict happens before price lookup.
        Assert.Empty(
            factory.TestPriceProvider
                .GetPriceCalls
        );

        await using var context =
            CreateDbContext();

        var product =
            await context
                .Set<RetailerProduct>()
                .SingleAsync();

        Assert.Equal(
            otherItemId,
            product.ItemId
        );

        Assert.Equal(
            0,
            await context
                .Set<Price>()
                .CountAsync()
        );
    }

    [Fact]
    public async Task Sync_WhenRepeated_UpdatesExistingProductAndPriceWithoutDuplicates()
    {
        var seeded =
            await SeedEnvironmentAsync(
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
            .ProductsByExternalId[
                "PRODUCT-1"
            ] =
                new ProviderProduct
                {
                    ExternalProductId =
                        "PRODUCT-1",

                    Name =
                        "Original Product Name",

                    Brand =
                        "Original Brand",

                    Size =
                        "1 gallon",

                    Upc =
                        "111111111111"
                };

        factory.TestPriceProvider
            .PricesByExternalProductId[
                "PRODUCT-1"
            ] =
                new ProviderPriceSnapshot
                {
                    RegularPrice =
                        5.00m,

                    SalePrice =
                        null,

                    MemberPrice =
                        null,

                    AvailabilityStatus =
                        AvailabilityStatus.Available,

                    SourceUpdatedAt =
                        DateTime.UtcNow
                };

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "repeat-product-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId =
                    seeded.ItemId,

                StoreLocationId =
                    seeded.StoreId,

                ExternalProductId =
                    "PRODUCT-1"
            };

        // First sync creates both rows.
        var firstResponse =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode
        );

        // Provider now reports refreshed metadata
        // and refreshed prices.
        factory.TestPriceProvider
            .ProductsByExternalId[
                "PRODUCT-1"
            ] =
                new ProviderProduct
                {
                    ExternalProductId =
                        "PRODUCT-1",

                    Name =
                        "Updated Product Name",

                    Brand =
                        "Updated Brand",

                    Size =
                        "96 fl oz",

                    Upc =
                        "222222222222"
                };

        factory.TestPriceProvider
            .PricesByExternalProductId[
                "PRODUCT-1"
            ] =
                new ProviderPriceSnapshot
                {
                    RegularPrice =
                        4.50m,

                    SalePrice =
                        3.75m,

                    MemberPrice =
                        3.25m,

                    AvailabilityStatus =
                        AvailabilityStatus.Available,

                    SourceUpdatedAt =
                        DateTime.UtcNow
                };

        // Act
        var secondResponse =
            await client.PostAsJsonAsync(
                "/api/product-sync",
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

        Assert.False(
            root.GetProperty("productCreated")
                .GetBoolean()
        );

        Assert.True(
            root.GetProperty("productUpdated")
                .GetBoolean()
        );

        Assert.True(
            root.GetProperty("priceSynced")
                .GetBoolean()
        );

        await using var context =
            CreateDbContext();

        var products =
            await context
                .Set<RetailerProduct>()
                .ToListAsync();

        Assert.Single(
            products
        );

        var product =
            products[0];

        Assert.Equal(
            "Updated Product Name",
            product.Name
        );

        Assert.Equal(
            "Updated Brand",
            product.Brand
        );

        Assert.Equal(
            "96 fl oz",
            product.Size
        );

        Assert.Equal(
            "222222222222",
            product.Upc
        );

        var prices =
            await context
                .Set<Price>()
                .ToListAsync();

        Assert.Single(
            prices
        );

        var price =
            prices[0];

        Assert.Equal(
            4.50m,
            price.RegularPrice
        );

        Assert.Equal(
            3.75m,
            price.SalePrice
        );

        Assert.Equal(
            3.25m,
            price.MemberPrice
        );

        Assert.Equal(
            2,
            factory.TestPriceProvider
                .GetProductCalls
                .Count
        );

        Assert.Equal(
            2,
            factory.TestPriceProvider
                .GetPriceCalls
                .Count
        );
    }

    [Fact]
    public async Task Sync_WhenProviderReturnsInvalidNegativePrice_ReturnsInternalServerError()
    {
        var seeded =
            await SeedEnvironmentAsync(
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
            .ProductsByExternalId[
                "PRODUCT-1"
            ] =
                new ProviderProduct
                {
                    ExternalProductId =
                        "PRODUCT-1",

                    Name =
                        "Whole Milk",

                    Brand =
                        "Test Brand",

                    Size =
                        "1 gallon",

                    Upc =
                        null
                };

        factory.TestPriceProvider
            .PricesByExternalProductId[
                "PRODUCT-1"
            ] =
                new ProviderPriceSnapshot
                {
                    RegularPrice =
                        -1.00m,

                    SalePrice =
                        null,

                    MemberPrice =
                        null,

                    AvailabilityStatus =
                        AvailabilityStatus.Available,

                    SourceUpdatedAt =
                        DateTime.UtcNow
                };

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "failed-price-product-sync@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                ItemId =
                    seeded.ItemId,

                StoreLocationId =
                    seeded.StoreId,

                ExternalProductId =
                    "PRODUCT-1"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/product-sync",
                request
            );

        // PriceService rejects the negative provider
        // price, so ProductPriceSyncService converts
        // that into PriceSyncFailed, and the controller
        // maps it to HTTP 500.
        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.True(
            document.RootElement
                .GetProperty("productCreated")
                .GetBoolean()
        );

        Assert.False(
            document.RootElement
                .GetProperty("priceSynced")
                .GetBoolean()
        );

        await using var context =
            CreateDbContext();

        // Product creation happens before price syncing.
        Assert.Equal(
            1,
            await context
                .Set<RetailerProduct>()
                .CountAsync()
        );

        // Invalid price must not be persisted.
        Assert.Equal(
            0,
            await context
                .Set<Price>()
                .CountAsync()
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
            CreateStore(
                retailer.Id
            );

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

        var item =
            CreateItem(
                itemName
            );

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return item.Id;
    }

    private async Task<(
        int RetailerId,
        int StoreId,
        int ItemId)>
        SeedEnvironmentAsync(
            string retailerName,
            string itemName)
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

        var item =
            CreateItem(
                itemName
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var store =
            CreateStore(
                retailer.Id
            );

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return (
            retailer.Id,
            store.Id,
            item.Id
        );
    }

    private async Task<int>
        SeedRetailerProductAsync(
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
                    "Existing Product",

                Brand =
                    "Existing Brand",

                Size =
                    "Existing Size",

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

    private static Item CreateItem(
        string name)
    {
        var now =
            DateTime.UtcNow;

        return new Item
        {
            Name =
                name,

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
    }

    private static StoreLocation CreateStore(
        int retailerId)
    {
        return new StoreLocation
        {
            RetailerId =
                retailerId,

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