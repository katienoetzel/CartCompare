using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.IntegrationTests.Fixtures;

namespace CartCompare.IntegrationTests.Api;

public class ComparisonsApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task CompareStore_WithoutToken_ReturnsUnauthorized()
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
                "/api/comparisons/stores/1"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task CompareStore_WhenStoreDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "missing-store-comparison@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/comparisons/stores/999999"
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
    public async Task CompareStore_WithCompleteList_ReturnsCorrectSubtotal()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "complete-comparison@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        var seeded =
            await SeedSingleComparisonAsync(
                userId: auth.UserId,
                quantity: 2,
                regularPrice: 4.00m,
                salePrice: 3.50m,
                memberPrice: null,
                addPrice: true
            );

        // Act
        var response =
            await client.GetAsync(
                $"/api/comparisons/stores/{seeded.StoreId}"
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
            JsonDocument.Parse(
                json
            );

        var root =
            document.RootElement;

        Assert.Equal(
            seeded.StoreId,
            root.GetProperty("storeLocationId")
                .GetInt32()
        );

        Assert.Equal(
            "Test Store",
            root.GetProperty("storeName")
                .GetString()
        );

        Assert.Equal(
            seeded.RetailerId,
            root.GetProperty("retailerId")
                .GetInt32()
        );

        Assert.True(
            root.GetProperty("isComplete")
                .GetBoolean()
        );

        Assert.Equal(
            7.00m,
            root.GetProperty("knownSubtotal")
                .GetDecimal()
        );

        Assert.Equal(
            0,
            root.GetProperty("missingItemCount")
                .GetInt32()
        );

        var items =
            root.GetProperty("items");

        Assert.Equal(
            1,
            items.GetArrayLength()
        );

        var item =
            items[0];

        Assert.Equal(
            seeded.ItemId,
            item.GetProperty("itemId")
                .GetInt32()
        );

        Assert.Equal(
            "Whole Milk",
            item.GetProperty("itemName")
                .GetString()
        );

        Assert.Equal(
            2,
            item.GetProperty("quantity")
                .GetInt32()
        );

        Assert.Equal(
            3.50m,
            item.GetProperty("unitPrice")
                .GetDecimal()
        );

        Assert.Equal(
            7.00m,
            item.GetProperty("lineTotal")
                .GetDecimal()
        );

        Assert.True(
            item.GetProperty("isAvailable")
                .GetBoolean()
        );
    }

    [Fact]
    public async Task CompareStore_WhenPriceIsMissing_MarksComparisonIncomplete()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "missing-price-comparison@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        var seeded =
            await SeedSingleComparisonAsync(
                userId: auth.UserId,
                quantity: 3,
                regularPrice: null,
                salePrice: null,
                memberPrice: null,
                addPrice: false
            );

        // Act
        var response =
            await client.GetAsync(
                $"/api/comparisons/stores/{seeded.StoreId}"
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
            JsonDocument.Parse(
                json
            );

        var root =
            document.RootElement;

        Assert.False(
            root.GetProperty("isComplete")
                .GetBoolean()
        );

        Assert.Equal(
            0m,
            root.GetProperty("knownSubtotal")
                .GetDecimal()
        );

        Assert.Equal(
            1,
            root.GetProperty("missingItemCount")
                .GetInt32()
        );

        var item =
            root.GetProperty("items")[0];

        Assert.False(
            item.GetProperty("isAvailable")
                .GetBoolean()
        );

        Assert.Equal(
            JsonValueKind.Null,
            item.GetProperty("unitPrice")
                .ValueKind
        );

        Assert.Equal(
            JsonValueKind.Null,
            item.GetProperty("lineTotal")
                .ValueKind
        );
    }

    [Fact]
    public async Task CompareStore_MemberPriceIsUsedOnlyAfterUserAddsMembership()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "member-price-comparison@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        var seeded =
            await SeedSingleComparisonAsync(
                userId: auth.UserId,
                quantity: 2,
                regularPrice: 5.00m,
                salePrice: null,
                memberPrice: 3.00m,
                addPrice: true
            );

        // First comparison: no membership.
        var withoutMembershipResponse =
            await client.GetAsync(
                $"/api/comparisons/stores/{seeded.StoreId}"
            );

        Assert.Equal(
            HttpStatusCode.OK,
            withoutMembershipResponse.StatusCode
        );

        var firstJson =
            await withoutMembershipResponse.Content
                .ReadAsStringAsync();

        using var firstDocument =
            JsonDocument.Parse(
                firstJson
            );

        var firstItem =
            firstDocument.RootElement
                .GetProperty("items")[0];

        Assert.Equal(
            5.00m,
            firstItem.GetProperty("unitPrice")
                .GetDecimal()
        );

        Assert.Equal(
            10.00m,
            firstDocument.RootElement
                .GetProperty("knownSubtotal")
                .GetDecimal()
        );

        // Give this user a membership with
        // the retailer through the real API.
        var membershipResponse =
            await client.PutAsync(
                $"/api/memberships/{seeded.RetailerId}",
                content: null
            );

        Assert.Equal(
            HttpStatusCode.OK,
            membershipResponse.StatusCode
        );

        // Act: compare the exact same store again.
        var withMembershipResponse =
            await client.GetAsync(
                $"/api/comparisons/stores/{seeded.StoreId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            withMembershipResponse.StatusCode
        );

        var secondJson =
            await withMembershipResponse.Content
                .ReadAsStringAsync();

        using var secondDocument =
            JsonDocument.Parse(
                secondJson
            );

        var secondItem =
            secondDocument.RootElement
                .GetProperty("items")[0];

        Assert.Equal(
            3.00m,
            secondItem.GetProperty("unitPrice")
                .GetDecimal()
        );

        Assert.Equal(
            6.00m,
            secondDocument.RootElement
                .GetProperty("knownSubtotal")
                .GetDecimal()
        );
    }

    [Fact]
    public async Task CompareStore_UsesCurrentUsersGroceryList()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var firstClient =
            factory.CreateClient();

        using var secondClient =
            factory.CreateClient();

        var firstAuth =
            await RegisterAndGetAuthAsync(
                firstClient,
                "first-comparison-user@example.com"
            );

        var secondAuth =
            await RegisterAndGetAuthAsync(
                secondClient,
                "second-comparison-user@example.com"
            );

        SetBearerToken(
            firstClient,
            firstAuth.Token
        );

        SetBearerToken(
            secondClient,
            secondAuth.Token
        );

        var seeded =
            await SeedSharedComparisonAsync(
                firstAuth.UserId,
                firstQuantity: 1,
                secondAuth.UserId,
                secondQuantity: 3
            );

        // Act
        var firstResponse =
            await firstClient.GetAsync(
                $"/api/comparisons/stores/{seeded.StoreId}"
            );

        var secondResponse =
            await secondClient.GetAsync(
                $"/api/comparisons/stores/{seeded.StoreId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode
        );

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode
        );

        var firstJson =
            await firstResponse.Content
                .ReadAsStringAsync();

        var secondJson =
            await secondResponse.Content
                .ReadAsStringAsync();

        using var firstDocument =
            JsonDocument.Parse(
                firstJson
            );

        using var secondDocument =
            JsonDocument.Parse(
                secondJson
            );

        Assert.Equal(
            2.00m,
            firstDocument.RootElement
                .GetProperty("knownSubtotal")
                .GetDecimal()
        );

        Assert.Equal(
            6.00m,
            secondDocument.RootElement
                .GetProperty("knownSubtotal")
                .GetDecimal()
        );

        Assert.Equal(
            1,
            firstDocument.RootElement
                .GetProperty("items")[0]
                .GetProperty("quantity")
                .GetInt32()
        );

        Assert.Equal(
            3,
            secondDocument.RootElement
                .GetProperty("items")[0]
                .GetProperty("quantity")
                .GetInt32()
        );
    }

    [Fact]
    public async Task CompareStores_RanksCompleteStoresAndReportsIncompleteAndMissingStores()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "multi-store-comparison@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        var seeded =
            await SeedMultiStoreComparisonAsync(
                auth.UserId
            );

        var request =
            new
            {
                StoreLocationIds =
                    new[]
                    {
                        seeded.FirstStoreId,
                        seeded.SecondStoreId,
                        seeded.IncompleteStoreId,
                        999999
                    }
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/comparisons/stores",
                request
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
            JsonDocument.Parse(
                json
            );

        var root =
            document.RootElement;

        Assert.Equal(
            2,
            root.GetProperty("completeStoreCount")
                .GetInt32()
        );

        Assert.Equal(
            1,
            root.GetProperty("incompleteStoreCount")
                .GetInt32()
        );

        var missingStoreIds =
            root.GetProperty(
                "missingStoreLocationIds"
            );

        Assert.Equal(
            1,
            missingStoreIds.GetArrayLength()
        );

        Assert.Equal(
            999999,
            missingStoreIds[0]
                .GetInt32()
        );

        var stores =
            root.GetProperty("stores");

        Assert.Equal(
            3,
            stores.GetArrayLength()
        );

        var firstStore =
            FindStore(
                stores,
                seeded.FirstStoreId
            );

        var secondStore =
            FindStore(
                stores,
                seeded.SecondStoreId
            );

        var incompleteStore =
            FindStore(
                stores,
                seeded.IncompleteStoreId
            );

        // First store costs $4.
        Assert.True(
            firstStore.GetProperty("isComplete")
                .GetBoolean()
        );

        Assert.Equal(
            4.00m,
            firstStore.GetProperty("knownSubtotal")
                .GetDecimal()
        );

        Assert.Equal(
            2,
            firstStore.GetProperty("rank")
                .GetInt32()
        );

        // Second store costs $3 and should rank #1.
        Assert.True(
            secondStore.GetProperty("isComplete")
                .GetBoolean()
        );

        Assert.Equal(
            3.00m,
            secondStore.GetProperty("knownSubtotal")
                .GetDecimal()
        );

        Assert.Equal(
            1,
            secondStore.GetProperty("rank")
                .GetInt32()
        );

        // Third store exists, but has no usable price.
        Assert.False(
            incompleteStore.GetProperty("isComplete")
                .GetBoolean()
        );

        Assert.Equal(
            1,
            incompleteStore.GetProperty("missingItemCount")
                .GetInt32()
        );

        Assert.Equal(
            JsonValueKind.Null,
            incompleteStore.GetProperty("rank")
                .ValueKind
        );
    }

    [Fact]
    public async Task CompareStores_WhenStoreIdIsNotPositive_ReturnsBadRequest()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "invalid-store-id-comparison@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        var request =
            new
            {
                StoreLocationIds =
                    new[]
                    {
                        1,
                        0
                    }
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/comparisons/stores",
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
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            "Store location IDs must be positive.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );
    }

    // -------------------------------------------------
    // Seeding helpers
    // -------------------------------------------------

    private async Task<(
        int RetailerId,
        int StoreId,
        int ItemId)>
        SeedSingleComparisonAsync(
            int userId,
            int quantity,
            decimal? regularPrice,
            decimal? salePrice,
            decimal? memberPrice,
            bool addPrice)
    {
        await using var context =
            CreateDbContext();

        var now =
            DateTime.UtcNow;

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var store =
            CreateStore(
                retailer.Id,
                "STORE-1",
                "Test Store"
            );

        var product =
            CreateProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1",
                "Test Milk"
            );

        context.Set<StoreLocation>()
            .Add(store);

        context.Set<RetailerProduct>()
            .Add(product);

        await context.SaveChangesAsync();

        context.Set<GroceryListItem>()
            .Add(
                new GroceryListItem
                {
                    UserId =
                        userId,

                    ItemId =
                        item.Id,

                    Quantity =
                        quantity,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                }
            );

        if (addPrice)
        {
            context.Set<Price>()
                .Add(
                    CreatePrice(
                        product.Id,
                        store.Id,
                        regularPrice,
                        salePrice,
                        memberPrice
                    )
                );
        }

        await context.SaveChangesAsync();

        return (
            retailer.Id,
            store.Id,
            item.Id
        );
    }

    private async Task<(
        int StoreId,
        int ItemId)>
        SeedSharedComparisonAsync(
            int firstUserId,
            int firstQuantity,
            int secondUserId,
            int secondQuantity)
    {
        await using var context =
            CreateDbContext();

        var now =
            DateTime.UtcNow;

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var store =
            CreateStore(
                retailer.Id,
                "STORE-1",
                "Test Store"
            );

        var product =
            CreateProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1",
                "Test Milk"
            );

        context.Set<StoreLocation>()
            .Add(store);

        context.Set<RetailerProduct>()
            .Add(product);

        await context.SaveChangesAsync();

        context.Set<GroceryListItem>()
            .AddRange(
                new GroceryListItem
                {
                    UserId =
                        firstUserId,

                    ItemId =
                        item.Id,

                    Quantity =
                        firstQuantity,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                },

                new GroceryListItem
                {
                    UserId =
                        secondUserId,

                    ItemId =
                        item.Id,

                    Quantity =
                        secondQuantity,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                }
            );

        context.Set<Price>()
            .Add(
                CreatePrice(
                    product.Id,
                    store.Id,
                    regularPrice: 2.00m,
                    salePrice: null,
                    memberPrice: null
                )
            );

        await context.SaveChangesAsync();

        return (
            store.Id,
            item.Id
        );
    }

    private async Task<(
        int FirstStoreId,
        int SecondStoreId,
        int IncompleteStoreId)>
        SeedMultiStoreComparisonAsync(
            int userId)
    {
        await using var context =
            CreateDbContext();

        var now =
            DateTime.UtcNow;

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var firstStore =
            CreateStore(
                retailer.Id,
                "STORE-1",
                "Store One"
            );

        var secondStore =
            CreateStore(
                retailer.Id,
                "STORE-2",
                "Store Two"
            );

        var incompleteStore =
            CreateStore(
                retailer.Id,
                "STORE-3",
                "Store Three"
            );

        var product =
            CreateProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1",
                "Test Milk"
            );

        context.Set<StoreLocation>()
            .AddRange(
                firstStore,
                secondStore,
                incompleteStore
            );

        context.Set<RetailerProduct>()
            .Add(product);

        await context.SaveChangesAsync();

        context.Set<GroceryListItem>()
            .Add(
                new GroceryListItem
                {
                    UserId =
                        userId,

                    ItemId =
                        item.Id,

                    Quantity =
                        1,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                }
            );

        // Store One: $4
        context.Set<Price>()
            .Add(
                CreatePrice(
                    product.Id,
                    firstStore.Id,
                    regularPrice: 4.00m,
                    salePrice: null,
                    memberPrice: null
                )
            );

        // Store Two: $3
        context.Set<Price>()
            .Add(
                CreatePrice(
                    product.Id,
                    secondStore.Id,
                    regularPrice: 3.00m,
                    salePrice: null,
                    memberPrice: null
                )
            );

        // Store Three intentionally receives no
        // Price row, making it incomplete.

        await context.SaveChangesAsync();

        return (
            firstStore.Id,
            secondStore.Id,
            incompleteStore.Id
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
                "Test Size",

            Category =
                "Test Category",

            IsActive =
                true,

            CreatedAt =
                now,

            UpdatedAt =
                now
        };
    }

    private static StoreLocation CreateStore(
        int retailerId,
        string externalLocationId,
        string name)
    {
        return new StoreLocation
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
    }

    private static RetailerProduct CreateProduct(
        int itemId,
        int retailerId,
        string externalProductId,
        string name)
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
                name,

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
    }

    private static Price CreatePrice(
        int retailerProductId,
        int storeLocationId,
        decimal? regularPrice,
        decimal? salePrice,
        decimal? memberPrice)
    {
        var now =
            DateTime.UtcNow;

        return new Price
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

    private static async Task<(
        string Token,
        int UserId)>
        RegisterAndGetAuthAsync(
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

        var root =
            document.RootElement;

        var token =
            root.GetProperty("token")
                .GetString();

        var userId =
            root.GetProperty("userId")
                .GetInt32();

        Assert.False(
            string.IsNullOrWhiteSpace(
                token
            )
        );

        Assert.True(
            userId > 0
        );

        return (
            token!,
            userId
        );
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

    // -------------------------------------------------
    // JSON helpers
    // -------------------------------------------------

    private static JsonElement FindStore(
        JsonElement stores,
        int storeLocationId)
    {
        foreach (
            var store
            in stores.EnumerateArray()
        )
        {
            if (
                store.GetProperty(
                    "storeLocationId"
                ).GetInt32()
                ==
                storeLocationId
            )
            {
                return store;
            }
        }

        throw new InvalidOperationException(
            $"Store {storeLocationId} "
            + "was not present in the response."
        );
    }
}