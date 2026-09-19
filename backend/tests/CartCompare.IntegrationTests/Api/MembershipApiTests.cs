using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Api;

public class MembershipApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task GetMemberships_WithoutToken_ReturnsUnauthorized()
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
                "/api/memberships"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Fact]
    public async Task AddMembership_WithValidRetailer_PersistsMembership()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                name: "Kroger",
                supportsMembership: true
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "membership-add-user@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        // Act
        var response =
            await client.PutAsync(
                $"/api/memberships/{retailerId}",
                content: null
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        await using var context =
            CreateDbContext();

        var membership =
            await context
                .Set<UserRetailerMembership>()
                .SingleOrDefaultAsync(
                    row =>
                        row.UserId ==
                            auth.UserId
                        &&
                        row.RetailerId ==
                            retailerId
                );

        Assert.NotNull(
            membership
        );
    }

    [Fact]
    public async Task GetMemberships_ReturnsCurrentUsersMemberships()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                name: "Kroger",
                supportsMembership: true
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "membership-get-user@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        var addResponse =
            await client.PutAsync(
                $"/api/memberships/{retailerId}",
                content: null
            );

        Assert.Equal(
            HttpStatusCode.OK,
            addResponse.StatusCode
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/memberships"
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

        Assert.Equal(
            JsonValueKind.Array,
            document.RootElement.ValueKind
        );

        Assert.Equal(
            1,
            document.RootElement
                .GetArrayLength()
        );
    }

    [Fact]
    public async Task GetMemberships_DoesNotReturnAnotherUsersMembership()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                name: "Kroger",
                supportsMembership: true
            );

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
                "first-membership-api-user@example.com"
            );

        var secondAuth =
            await RegisterAndGetAuthAsync(
                secondClient,
                "second-membership-api-user@example.com"
            );

        SetBearerToken(
            firstClient,
            firstAuth.Token
        );

        SetBearerToken(
            secondClient,
            secondAuth.Token
        );

        var addResponse =
            await firstClient.PutAsync(
                $"/api/memberships/{retailerId}",
                content: null
            );

        Assert.Equal(
            HttpStatusCode.OK,
            addResponse.StatusCode
        );

        // Act
        var firstResponse =
            await firstClient.GetAsync(
                "/api/memberships"
            );

        var secondResponse =
            await secondClient.GetAsync(
                "/api/memberships"
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
            0,
            secondDocument.RootElement
                .GetArrayLength()
        );

        // Also verify there is only one database row,
        // and that it belongs to User A.
        await using var context =
            CreateDbContext();

        var rows =
            await context
                .Set<UserRetailerMembership>()
                .ToListAsync();

        Assert.Single(
            rows
        );

        Assert.Equal(
            firstAuth.UserId,
            rows[0].UserId
        );

        Assert.NotEqual(
            secondAuth.UserId,
            rows[0].UserId
        );
    }

    [Fact]
    public async Task AddMembership_WhenMembershipAlreadyExists_ReturnsOkAndDoesNotDuplicateRow()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                name: "Kroger",
                supportsMembership: true
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "duplicate-membership-user@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        var firstResponse =
            await client.PutAsync(
                $"/api/memberships/{retailerId}",
                content: null
            );

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode
        );

        // Act
        var secondResponse =
            await client.PutAsync(
                $"/api/memberships/{retailerId}",
                content: null
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode
        );

        await using var context =
            CreateDbContext();

        var rows =
            await context
                .Set<UserRetailerMembership>()
                .Where(
                    row =>
                        row.UserId ==
                            auth.UserId
                        &&
                        row.RetailerId ==
                            retailerId
                )
                .ToListAsync();

        Assert.Single(
            rows
        );
    }

    [Fact]
    public async Task AddMembership_WhenRetailerDoesNotExist_ReturnsNotFound()
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
                "missing-retailer-membership@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        // Act
        var response =
            await client.PutAsync(
                "/api/memberships/999999",
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
            "Retailer not found.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );
    }

    [Fact]
    public async Task AddMembership_WhenRetailerDoesNotSupportMembership_ReturnsBadRequest()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                name: "No Membership Retailer",
                supportsMembership: false
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "unsupported-membership-user@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        // Act
        var response =
            await client.PutAsync(
                $"/api/memberships/{retailerId}",
                content: null
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
            "This retailer does not support memberships.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        await using var context =
            CreateDbContext();

        Assert.False(
            await context
                .Set<UserRetailerMembership>()
                .AnyAsync()
        );
    }

    [Fact]
    public async Task RemoveMembership_WhenMembershipExists_ReturnsNoContentAndRemovesIt()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                name: "Kroger",
                supportsMembership: true
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "remove-membership-user@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        var addResponse =
            await client.PutAsync(
                $"/api/memberships/{retailerId}",
                content: null
            );

        Assert.Equal(
            HttpStatusCode.OK,
            addResponse.StatusCode
        );

        // Act
        var deleteResponse =
            await client.DeleteAsync(
                $"/api/memberships/{retailerId}"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode
        );

        await using var context =
            CreateDbContext();

        Assert.False(
            await context
                .Set<UserRetailerMembership>()
                .AnyAsync(
                    row =>
                        row.UserId ==
                            auth.UserId
                        &&
                        row.RetailerId ==
                            retailerId
                )
        );
    }

    [Fact]
    public async Task RemoveMembership_WhenMembershipDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                name: "Kroger",
                supportsMembership: true
            );

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var auth =
            await RegisterAndGetAuthAsync(
                client,
                "missing-membership-user@example.com"
            );

        SetBearerToken(
            client,
            auth.Token
        );

        // Act
        var response =
            await client.DeleteAsync(
                $"/api/memberships/{retailerId}"
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

    private async Task<int> SeedRetailerAsync(
        string name,
        bool supportsMembership)
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
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        return retailer.Id;
    }

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
}