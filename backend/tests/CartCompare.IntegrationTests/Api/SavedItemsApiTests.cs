using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;

namespace CartCompare.IntegrationTests.Api;

public class SavedItemsApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task GetSavedItems_WithoutToken_ReturnsUnauthorized()
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
                "/api/saveditems"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task SaveItem_WithValidToken_PersistsAndReturnsSavedItem()
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
                "saved-item-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.PostAsync(
                $"/api/saveditems/{itemId}",
                content: null
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

        // Verify it really persisted.
        await using var context =
            CreateDbContext();

        var savedItem =
            await context
                .Set<SavedItem>()
                .FindAsync(
                    root.GetProperty("id")
                        .GetInt32()
                );

        Assert.NotNull(
            savedItem
        );

        Assert.Equal(
            itemId,
            savedItem.ItemId
        );
    }

    [Fact]
    public async Task SaveItem_WhenItemDoesNotExist_ReturnsNotFound()
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
                "missing-item-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.PostAsync(
                "/api/saveditems/999999",
                content: null
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
    public async Task GetSavedItems_ReturnsOnlyCurrentUsersItems()
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
                "first-saved-user@example.com"
            );

        var secondToken =
            await RegisterAndGetTokenAsync(
                secondClient,
                "second-saved-user@example.com"
            );

        SetBearerToken(
            firstClient,
            firstToken
        );

        SetBearerToken(
            secondClient,
            secondToken
        );

        var firstSaveResponse =
            await firstClient.PostAsync(
                $"/api/saveditems/{firstItemId}",
                content: null
            );

        Assert.Equal(
            HttpStatusCode.OK,
            firstSaveResponse.StatusCode
        );

        var secondSaveResponse =
            await secondClient.PostAsync(
                $"/api/saveditems/{secondItemId}",
                content: null
            );

        Assert.Equal(
            HttpStatusCode.OK,
            secondSaveResponse.StatusCode
        );

        // Act
        var firstResponse =
            await firstClient.GetAsync(
                "/api/saveditems"
            );

        var secondResponse =
            await secondClient.GetAsync(
                "/api/saveditems"
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
            "Large Eggs",
            secondDocument.RootElement[0]
                .GetProperty("name")
                .GetString()
        );
    }

    [Fact]
    public async Task RemoveSavedItem_WhenSavedItemExists_ReturnsNoContentAndRemovesIt()
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
                "remove-saved-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var saveResponse =
            await client.PostAsync(
                $"/api/saveditems/{itemId}",
                content: null
            );

        Assert.Equal(
            HttpStatusCode.OK,
            saveResponse.StatusCode
        );

        // Act
        var deleteResponse =
            await client.DeleteAsync(
                $"/api/saveditems/{itemId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode
        );

        var getResponse =
            await client.GetAsync(
                "/api/saveditems"
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
    }

    [Fact]
    public async Task RemoveSavedItem_WhenSavedItemDoesNotExist_ReturnsNotFound()
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
                "unsaved-item-user@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.DeleteAsync(
                $"/api/saveditems/{itemId}"
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