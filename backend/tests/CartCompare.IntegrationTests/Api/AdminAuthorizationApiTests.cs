using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using CartCompare.IntegrationTests.Fixtures;

namespace CartCompare.IntegrationTests.Api;

public class AdminAuthorizationApiTests
    : PostgresIntegrationTestBase
{
    // =================================================
    // ALL ADMIN-ONLY ENDPOINTS
    // =================================================

    [Theory]
    [InlineData(
        "GET",
        "/api/integrations/kroger/status"
    )]
    [InlineData(
        "GET",
        "/api/product-candidates?itemId=1&storeLocationId=1"
    )]
    [InlineData(
        "POST",
        "/api/product-sync"
    )]
    [InlineData(
        "POST",
        "/api/store-sync"
    )]
    [InlineData(
        "POST",
        "/api/items"
    )]
    [InlineData(
        "POST",
        "/api/retailers"
    )]
    [InlineData(
        "POST",
        "/api/store-locations"
    )]
    [InlineData(
        "POST",
        "/api/retailer-products"
    )]
    [InlineData(
        "PUT",
        "/api/prices"
    )]
    public async Task AdminOnlyEndpoint_WithoutToken_ReturnsUnauthorized(
        string method,
        string url)
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString,
                strictAdminAuthorization: true
            );

        using var client =
            factory.CreateClient();

        using var request =
            CreateRequest(
                method,
                url
            );

        var response =
            await client.SendAsync(
                request
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    [Theory]
    [InlineData(
        "GET",
        "/api/integrations/kroger/status"
    )]
    [InlineData(
        "GET",
        "/api/product-candidates?itemId=1&storeLocationId=1"
    )]
    [InlineData(
        "POST",
        "/api/product-sync"
    )]
    [InlineData(
        "POST",
        "/api/store-sync"
    )]
    [InlineData(
        "POST",
        "/api/items"
    )]
    [InlineData(
        "POST",
        "/api/retailers"
    )]
    [InlineData(
        "POST",
        "/api/store-locations"
    )]
    [InlineData(
        "POST",
        "/api/retailer-products"
    )]
    [InlineData(
        "PUT",
        "/api/prices"
    )]
    public async Task AdminOnlyEndpoint_WithNormalUser_ReturnsForbidden(
        string method,
        string url)
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString,
                strictAdminAuthorization: true
            );

        using var client =
            factory.CreateClient();

        var normalUserEmail =
            $"normal-security-{Guid.NewGuid():N}@example.com";

        var token =
            await RegisterAndGetTokenAsync(
                client,
                normalUserEmail
            );

        SetBearerToken(
            client,
            token
        );

        using var request =
            CreateRequest(
                method,
                url
            );

        var response =
            await client.SendAsync(
                request
            );

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode
        );
    }

    // =================================================
    // CONFIGURED ADMIN
    // =================================================

    [Fact]
    public async Task AdminOnlyEndpoint_WithConfiguredAdmin_AllowsRequest()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString,
                strictAdminAuthorization: true
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                CartCompareWebApplicationFactory
                    .TestAdminEmail
            );

        SetBearerToken(
            client,
            token
        );

        var request =
            new
            {
                Name =
                    "Admin Security Test Milk",

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
            "Admin Security Test Milk",
            document.RootElement
                .GetProperty("name")
                .GetString()
        );
    }

    // =================================================
    // REQUEST HELPERS
    // =================================================

    private static HttpRequestMessage CreateRequest(
        string method,
        string url)
    {
        var request =
            new HttpRequestMessage(
                new HttpMethod(method),
                url
            );

        if (
            method == "POST"
            ||
            method == "PUT"
        )
        {
            request.Content =
                new StringContent(
                    "{}",
                    Encoding.UTF8,
                    "application/json"
                );
        }

        return request;
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