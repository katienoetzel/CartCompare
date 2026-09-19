using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Api;

public class GroceryListApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task GetItems_WithoutToken_ReturnsUnauthorized()
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
                "/api/grocery-list"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task SetQuantity_WithValidRequest_CreatesGroceryListItem()
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

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "create-grocery-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                Quantity = 3
            };

        // Act
        var response =
            await client.PutAsJsonAsync(
                $"/api/grocery-list/{itemId}",
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

        Assert.True(
            root.GetProperty("id")
                .GetInt32() > 0
        );

        Assert.Equal(
            itemId,
            root.GetProperty("itemId")
                .GetInt32()
        );

        Assert.Equal(
            "Whole Milk",
            root.GetProperty("name")
                .GetString()
        );

        Assert.Equal(
            3,
            root.GetProperty("quantity")
                .GetInt32()
        );

        // Verify it actually persisted.
        await using var context =
            CreateDbContext();

        var stored =
            await context
                .Set<GroceryListItem>()
                .SingleAsync();

        Assert.Equal(
            itemId,
            stored.ItemId
        );

        Assert.Equal(
            3,
            stored.Quantity
        );
    }

    [Fact]
    public async Task SetQuantity_WhenItemAlreadyExists_UpdatesExistingRow()
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

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "update-grocery-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var firstResponse =
            await client.PutAsJsonAsync(
                $"/api/grocery-list/{itemId}",
                new
                {
                    Quantity = 2
                }
            );

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode
        );

        // Act
        var secondResponse =
            await client.PutAsJsonAsync(
                $"/api/grocery-list/{itemId}",
                new
                {
                    Quantity = 7
                }
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
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            7,
            document.RootElement
                .GetProperty("quantity")
                .GetInt32()
        );

        await using var context =
            CreateDbContext();

        var rows =
            await context
                .Set<GroceryListItem>()
                .Where(
                    row =>
                        row.ItemId ==
                        itemId
                )
                .ToListAsync();

        // Updating quantity should not create
        // a second grocery-list row.
        Assert.Single(
            rows
        );

        Assert.Equal(
            7,
            rows[0].Quantity
        );
    }

    [Fact]
    public async Task SetQuantity_WhenItemDoesNotExist_ReturnsNotFound()
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
                "missing-grocery-item@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.PutAsJsonAsync(
                "/api/grocery-list/999999",
                new
                {
                    Quantity = 2
                }
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
            "Item not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );
    }

    [Fact]
    public async Task SetQuantity_WhenQuantityIsZero_ReturnsBadRequest()
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

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "zero-quantity-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.PutAsJsonAsync(
                $"/api/grocery-list/{itemId}",
                new
                {
                    Quantity = 0
                }
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        // Invalid model input should never have
        // created a database row.
        await using var context =
            CreateDbContext();

        Assert.False(
            await context
                .Set<GroceryListItem>()
                .AnyAsync()
        );
    }

    [Fact]
    public async Task SetQuantity_WhenQuantityIsNegative_ReturnsBadRequest()
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

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "negative-quantity-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.PutAsJsonAsync(
                $"/api/grocery-list/{itemId}",
                new
                {
                    Quantity = -2
                }
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        Assert.False(
            await context
                .Set<GroceryListItem>()
                .AnyAsync()
        );
    }

    [Fact]
    public async Task GetItems_ReturnsOnlyCurrentUsersGroceryList()
    {
        // Arrange
        var firstItemId =
            await SeedItemAsync(
                "Whole Milk"
            );

        var secondItemId =
            await SeedItemAsync(
                "Large Eggs"
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var firstClient =
            factory.CreateClient();

        using var secondClient =
            factory.CreateClient();

        var firstToken =
            await RegisterAndGetTokenAsync(
                firstClient,
                "first-grocery-api-user@example.com"
            );

        var secondToken =
            await RegisterAndGetTokenAsync(
                secondClient,
                "second-grocery-api-user@example.com"
            );

        SetBearerToken(
            firstClient,
            firstToken
        );

        SetBearerToken(
            secondClient,
            secondToken
        );

        var firstPutResponse =
            await firstClient.PutAsJsonAsync(
                $"/api/grocery-list/{firstItemId}",
                new
                {
                    Quantity = 2
                }
            );

        Assert.Equal(
            HttpStatusCode.OK,
            firstPutResponse.StatusCode
        );

        var secondPutResponse =
            await secondClient.PutAsJsonAsync(
                $"/api/grocery-list/{secondItemId}",
                new
                {
                    Quantity = 5
                }
            );

        Assert.Equal(
            HttpStatusCode.OK,
            secondPutResponse.StatusCode
        );

        // Act
        var firstResponse =
            await firstClient.GetAsync(
                "/api/grocery-list"
            );

        var secondResponse =
            await secondClient.GetAsync(
                "/api/grocery-list"
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
            1,
            firstDocument.RootElement
                .GetArrayLength()
        );

        Assert.Equal(
            firstItemId,
            firstDocument.RootElement[0]
                .GetProperty("itemId")
                .GetInt32()
        );

        Assert.Equal(
            2,
            firstDocument.RootElement[0]
                .GetProperty("quantity")
                .GetInt32()
        );

        Assert.Equal(
            "Whole Milk",
            firstDocument.RootElement[0]
                .GetProperty("name")
                .GetString()
        );

        Assert.Equal(
            1,
            secondDocument.RootElement
                .GetArrayLength()
        );

        Assert.Equal(
            secondItemId,
            secondDocument.RootElement[0]
                .GetProperty("itemId")
                .GetInt32()
        );

        Assert.Equal(
            5,
            secondDocument.RootElement[0]
                .GetProperty("quantity")
                .GetInt32()
        );

        Assert.Equal(
            "Large Eggs",
            secondDocument.RootElement[0]
                .GetProperty("name")
                .GetString()
        );
    }

    [Fact]
    public async Task RemoveItem_WhenItemExists_ReturnsNoContentAndRemovesIt()
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

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "remove-grocery-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var putResponse =
            await client.PutAsJsonAsync(
                $"/api/grocery-list/{itemId}",
                new
                {
                    Quantity = 3
                }
            );

        Assert.Equal(
            HttpStatusCode.OK,
            putResponse.StatusCode
        );

        // Act
        var deleteResponse =
            await client.DeleteAsync(
                $"/api/grocery-list/{itemId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode
        );

        var getResponse =
            await client.GetAsync(
                "/api/grocery-list"
            );

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode
        );

        var json =
            await getResponse.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            0,
            document.RootElement
                .GetArrayLength()
        );

        await using var context =
            CreateDbContext();

        Assert.False(
            await context
                .Set<GroceryListItem>()
                .AnyAsync()
        );
    }

    [Fact]
    public async Task RemoveItem_WhenItemIsNotOnList_ReturnsNotFound()
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

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "missing-grocery-row-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.DeleteAsync(
                $"/api/grocery-list/{itemId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    private async Task<int> SeedItemAsync(
        string name)
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