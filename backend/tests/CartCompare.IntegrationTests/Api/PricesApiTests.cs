using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Api;

public class PricesApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task GetById_WhenPriceDoesNotExist_ReturnsNotFound()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                "/api/prices/999999"
            );

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );
    }

    [Fact]
    public async Task GetById_WhenPriceExists_ReturnsStoredPrice()
    {
        var seeded =
            await SeedProductStoreAndPriceAsync(
                regularPrice: 4.29m,
                salePrice: 3.99m,
                memberPrice: 3.49m
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                $"/api/prices/{seeded.PriceId}"
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(
                json
            );

        var root =
            document.RootElement;

        Assert.Equal(
            seeded.PriceId,
            root.GetProperty("id")
                .GetInt32()
        );

        Assert.Equal(
            seeded.ProductId,
            root.GetProperty("retailerProductId")
                .GetInt32()
        );

        Assert.Equal(
            seeded.StoreId,
            root.GetProperty("storeLocationId")
                .GetInt32()
        );

        Assert.Equal(
            4.29m,
            root.GetProperty("regularPrice")
                .GetDecimal()
        );

        Assert.Equal(
            3.99m,
            root.GetProperty("salePrice")
                .GetDecimal()
        );

        Assert.Equal(
            3.49m,
            root.GetProperty("memberPrice")
                .GetDecimal()
        );

        Assert.Equal(
            "IntegrationTest",
            root.GetProperty("sourceProvider")
                .GetString()
        );
    }

    [Fact]
    public async Task GetByProductAndStore_WhenPriceExists_ReturnsPrice()
    {
        var seeded =
            await SeedProductStoreAndPriceAsync(
                regularPrice: 5.25m
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                $"/api/prices/product/{seeded.ProductId}"
                + $"/store/{seeded.StoreId}"
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            seeded.PriceId,
            document.RootElement
                .GetProperty("id")
                .GetInt32()
        );

        Assert.Equal(
            5.25m,
            document.RootElement
                .GetProperty("regularPrice")
                .GetDecimal()
        );
    }

    [Fact]
    public async Task GetByProduct_ReturnsPricesForProduct()
    {
        var seeded =
            await SeedProductWithTwoStorePricesAsync();

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                $"/api/prices/product/{seeded.ProductId}"
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            JsonValueKind.Array,
            document.RootElement.ValueKind
        );

        Assert.Equal(
            2,
            document.RootElement
                .GetArrayLength()
        );
    }

    [Fact]
    public async Task Upsert_WithoutToken_ReturnsUnauthorized()
    {
        var seeded =
            await SeedProductAndStoreAsync();

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var request =
            CreateUpsertRequest(
                seeded.ProductId,
                seeded.StoreId,
                regularPrice: 4.00m
            );

        var response =
            await client.PutAsJsonAsync(
                "/api/prices",
                request
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task Upsert_WithValidRequest_CreatesPrice()
    {
        var seeded =
            await SeedProductAndStoreAsync();

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "create-price-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateUpsertRequest(
                seeded.ProductId,
                seeded.StoreId,
                regularPrice: 4.50m,
                salePrice: 3.99m,
                memberPrice: 3.49m
            );

        var response =
            await client.PutAsJsonAsync(
                "/api/prices",
                request
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        var stored =
            await context
                .Set<Price>()
                .SingleAsync();

        Assert.Equal(
            4.50m,
            stored.RegularPrice
        );

        Assert.Equal(
            3.99m,
            stored.SalePrice
        );

        Assert.Equal(
            3.49m,
            stored.MemberPrice
        );

        Assert.Equal(
            seeded.ProductId,
            stored.RetailerProductId
        );

        Assert.Equal(
            seeded.StoreId,
            stored.StoreLocationId
        );
    }

    [Fact]
    public async Task Upsert_WhenPriceAlreadyExists_UpdatesInsteadOfCreatingDuplicate()
    {
        var seeded =
            await SeedProductStoreAndPriceAsync(
                regularPrice: 5.00m
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
                "update-price-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateUpsertRequest(
                seeded.ProductId,
                seeded.StoreId,
                regularPrice: 4.00m,
                salePrice: 3.50m
            );

        var response =
            await client.PutAsJsonAsync(
                "/api/prices",
                request
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        var prices =
            await context
                .Set<Price>()
                .ToListAsync();

        Assert.Single(
            prices
        );

        Assert.Equal(
            seeded.PriceId,
            prices[0].Id
        );

        Assert.Equal(
            4.00m,
            prices[0].RegularPrice
        );

        Assert.Equal(
            3.50m,
            prices[0].SalePrice
        );
    }

    [Fact]
    public async Task Upsert_WithNegativePrice_ReturnsBadRequestAndDoesNotPersist()
    {
        var seeded =
            await SeedProductAndStoreAsync();

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "negative-price-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateUpsertRequest(
                seeded.ProductId,
                seeded.StoreId,
                regularPrice: -1.00m
            );

        var response =
            await client.PutAsJsonAsync(
                "/api/prices",
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
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            "Price values cannot be negative.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        await using var context =
            CreateDbContext();

        Assert.False(
            await context
                .Set<Price>()
                .AnyAsync()
        );
    }

    [Fact]
    public async Task Upsert_WhenRetailerProductDoesNotExist_ReturnsNotFound()
    {
        var storeId =
            await SeedStoreAsync(
                retailerName: "Kroger"
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
                "missing-product-price-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateUpsertRequest(
                retailerProductId: 999999,
                storeLocationId: storeId,
                regularPrice: 4.00m
            );

        var response =
            await client.PutAsJsonAsync(
                "/api/prices",
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
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            "Retailer product not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );
    }

    [Fact]
    public async Task Upsert_WhenStoreDoesNotExist_ReturnsNotFound()
    {
        var productId =
            await SeedProductAsync(
                retailerName: "Kroger"
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
                "missing-store-price-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateUpsertRequest(
                productId,
                storeLocationId: 999999,
                regularPrice: 4.00m
            );

        var response =
            await client.PutAsJsonAsync(
                "/api/prices",
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
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            "Store location not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );
    }

    [Fact]
    public async Task Upsert_WhenProductAndStoreBelongToDifferentRetailers_ReturnsBadRequest()
    {
        var seeded =
            await SeedMismatchedProductAndStoreAsync();

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "retailer-mismatch-price-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            CreateUpsertRequest(
                seeded.ProductId,
                seeded.StoreId,
                regularPrice: 4.00m
            );

        var response =
            await client.PutAsJsonAsync(
                "/api/prices",
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
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            "The retailer product and store location belong to different retailers.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );
    }

    [Fact]
    public async Task EffectivePrice_WithoutToken_ReturnsUnauthorized()
    {
        var seeded =
            await SeedProductStoreAndPriceAsync(
                regularPrice: 5.00m,
                memberPrice: 3.00m
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                $"/api/prices/product/{seeded.ProductId}"
                + $"/store/{seeded.StoreId}/effective"
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task EffectivePrice_WithValidToken_ReturnsSuccess()
    {
        var seeded =
            await SeedProductStoreAndPriceAsync(
                regularPrice: 5.00m,
                salePrice: 4.00m,
                memberPrice: 3.00m
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
                "effective-price-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var response =
            await client.GetAsync(
                $"/api/prices/product/{seeded.ProductId}"
                + $"/store/{seeded.StoreId}/effective"
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        // We already test exact effective-price selection
        // extensively in PriceService and Comparisons API.
        // Here the purpose is to prove this authenticated
        // HTTP route reaches that service successfully.
        var json =
            await response.Content
                .ReadAsStringAsync();

        Assert.False(
            string.IsNullOrWhiteSpace(
                json
            )
        );
    }

    // -------------------------------------------------
    // Request helpers
    // -------------------------------------------------

    private static object CreateUpsertRequest(
        int retailerProductId,
        int storeLocationId,
        decimal? regularPrice = null,
        decimal? salePrice = null,
        decimal? memberPrice = null)
    {
        return new
        {
            RetailerProductId =
                retailerProductId,

            StoreLocationId =
                storeLocationId,

            RegularPrice =
                regularPrice,

            SalePrice =
                salePrice,

            MemberPrice =
                memberPrice,

            AvailabilityStatus =
                AvailabilityStatus.Available,

            SourceProvider =
                "IntegrationTest",

            SourceUpdatedAt =
                DateTime.UtcNow
        };
    }

    // -------------------------------------------------
    // Database seed helpers
    // -------------------------------------------------

    private async Task<(
        int ProductId,
        int StoreId)>
        SeedProductAndStoreAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        var item =
            CreateItem();

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var product =
            CreateProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        var store =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return (
            product.Id,
            store.Id
        );
    }

    private async Task<(
        int ProductId,
        int StoreId,
        int PriceId)>
        SeedProductStoreAndPriceAsync(
            decimal? regularPrice = null,
            decimal? salePrice = null,
            decimal? memberPrice = null)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        var item =
            CreateItem();

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var product =
            CreateProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        var store =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        var price =
            CreatePrice(
                product.Id,
                store.Id,
                regularPrice,
                salePrice,
                memberPrice
            );

        context.Set<Price>()
            .Add(price);

        await context.SaveChangesAsync();

        return (
            product.Id,
            store.Id,
            price.Id
        );
    }

    private async Task<(
        int ProductId,
        int FirstStoreId,
        int SecondStoreId)>
        SeedProductWithTwoStorePricesAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        var item =
            CreateItem();

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var product =
            CreateProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        var firstStore =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        var secondStore =
            CreateStore(
                retailer.Id,
                "STORE-2"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        context.Set<StoreLocation>()
            .AddRange(
                firstStore,
                secondStore
            );

        await context.SaveChangesAsync();

        context.Set<Price>()
            .AddRange(
                CreatePrice(
                    product.Id,
                    firstStore.Id,
                    4.00m,
                    null,
                    null
                ),

                CreatePrice(
                    product.Id,
                    secondStore.Id,
                    5.00m,
                    null,
                    null
                )
            );

        await context.SaveChangesAsync();

        return (
            product.Id,
            firstStore.Id,
            secondStore.Id
        );
    }

    private async Task<int> SeedStoreAsync(
        string retailerName)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                retailerName
            );

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        var store =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return store.Id;
    }

    private async Task<int> SeedProductAsync(
        string retailerName)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                retailerName
            );

        var item =
            CreateItem();

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var product =
            CreateProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        await context.SaveChangesAsync();

        return product.Id;
    }

    private async Task<(
        int ProductId,
        int StoreId)>
        SeedMismatchedProductAndStoreAsync()
    {
        await using var context =
            CreateDbContext();

        var firstRetailer =
            CreateRetailer(
                "Kroger"
            );

        var secondRetailer =
            CreateRetailer(
                "Other Retailer"
            );

        var item =
            CreateItem();

        context.Set<Retailer>()
            .AddRange(
                firstRetailer,
                secondRetailer
            );

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var product =
            CreateProduct(
                item.Id,
                firstRetailer.Id,
                "PRODUCT-1"
            );

        var store =
            CreateStore(
                secondRetailer.Id,
                "STORE-1"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return (
            product.Id,
            store.Id
        );
    }

    // -------------------------------------------------
    // Entity helpers
    // -------------------------------------------------

    private static Retailer CreateRetailer(
        string name)
    {
        return new Retailer
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
    }

    private static Item CreateItem()
    {
        var now =
            DateTime.UtcNow;

        return new Item
        {
            Name =
                "Whole Milk",

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

    private static RetailerProduct CreateProduct(
        int itemId,
        int retailerId,
        string externalProductId)
    {
        return new RetailerProduct
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
    }

    private static StoreLocation CreateStore(
        int retailerId,
        string externalLocationId)
    {
        return new StoreLocation
        {
            RetailerId =
                retailerId,

            ExternalLocationId =
                externalLocationId,

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

    private static Price CreatePrice(
        int productId,
        int storeId,
        decimal? regularPrice,
        decimal? salePrice,
        decimal? memberPrice)
    {
        var now =
            DateTime.UtcNow;

        return new Price
        {
            RetailerProductId =
                productId,

            StoreLocationId =
                storeId,

            RegularPrice =
                regularPrice,

            SalePrice =
                salePrice,

            MemberPrice =
                memberPrice,

            AvailabilityStatus =
                AvailabilityStatus.Available,

            SourceProvider =
                "IntegrationTest",

            SourceUpdatedAt =
                now,

            LastCheckedAt =
                now,

            UpdatedAt =
                now
        };
    }

    // -------------------------------------------------
    // Authentication helpers
    // -------------------------------------------------

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
            JsonDocument.Parse(
                json
            );

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