using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Api;

public class AuthApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task Register_WithValidRequest_CreatesUserAndReturnsToken()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var request =
            CreateRegisterRequest(
                email:
                    "register-user@example.com"
            );

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/auth/register",
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

        Assert.False(
            string.IsNullOrWhiteSpace(
                root.GetProperty("token")
                    .GetString()
            )
        );

        Assert.True(
            root.GetProperty("userId")
                .GetInt32() > 0
        );

        Assert.Equal(
            "register-user@example.com",
            root.GetProperty("email")
                .GetString()
        );

        Assert.Equal(
            "Katie",
            root.GetProperty("firstName")
                .GetString()
        );

        Assert.Equal(
            "Tester",
            root.GetProperty("lastName")
                .GetString()
        );

        Assert.True(
            root.GetProperty("expiresAt")
                .GetDateTime() >
            DateTime.UtcNow
        );

        // Also verify Identity actually persisted
        // the user in PostgreSQL.
        await using var context =
            CreateDbContext();

        var storedUser =
            await context
                .Set<ApplicationUser>()
                .SingleOrDefaultAsync(
                    user =>
                        user.Email ==
                        "register-user@example.com"
                );

        Assert.NotNull(
            storedUser
        );

        Assert.Equal(
            "Katie",
            storedUser.FirstName
        );

        Assert.Equal(
            "Tester",
            storedUser.LastName
        );

        Assert.Equal(
            "27606",
            storedUser.DefaultPostalCode
        );
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ReturnsBadRequest()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var request =
            CreateRegisterRequest(
                email:
                    "duplicate-user@example.com"
            );

        var firstResponse =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                request
            );

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode
        );

        // Act
        var secondResponse =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                request
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            secondResponse.StatusCode
        );

        var json =
            await secondResponse.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(
                json
            );

        Assert.True(
            document.RootElement
                .TryGetProperty(
                    "errors",
                    out _
                )
        );
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        const string email =
            "wrong-password-user@example.com";

        var registerResponse =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                CreateRegisterRequest(
                    email
                )
            );

        Assert.Equal(
            HttpStatusCode.OK,
            registerResponse.StatusCode
        );

        var loginRequest =
            new
            {
                Email =
                    email,

                Password =
                    "DefinitelyWrong123!"
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                loginRequest
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
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
            "Invalid email or password.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        const string email =
            "login-user@example.com";

        const string password =
            "CartCompare123!";

        var registerResponse =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                CreateRegisterRequest(
                    email,
                    password
                )
            );

        Assert.Equal(
            HttpStatusCode.OK,
            registerResponse.StatusCode
        );

        var loginRequest =
            new
            {
                Email =
                    email,

                Password =
                    password
            };

        // Act
        var response =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                loginRequest
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
            string.IsNullOrWhiteSpace(
                root.GetProperty("token")
                    .GetString()
            )
        );

        Assert.True(
            root.GetProperty("userId")
                .GetInt32() > 0
        );

        Assert.Equal(
            email,
            root.GetProperty("email")
                .GetString()
        );

        Assert.Equal(
            "Katie",
            root.GetProperty("firstName")
                .GetString()
        );

        Assert.Equal(
            "Tester",
            root.GetProperty("lastName")
                .GetString()
        );
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
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
    public async Task ProtectedEndpoint_WithValidToken_ReturnsSuccess()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var registerResponse =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                CreateRegisterRequest(
                    "authorized-user@example.com"
                )
            );

        Assert.Equal(
            HttpStatusCode.OK,
            registerResponse.StatusCode
        );

        var registerJson =
            await registerResponse.Content
                .ReadAsStringAsync();

        using var registerDocument =
            JsonDocument.Parse(
                registerJson
            );

        var token =
            registerDocument.RootElement
                .GetProperty("token")
                .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(
                token
            )
        );

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token
            );

        // Act
        var response =
            await client.GetAsync(
                "/api/grocery-list"
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

        // Newly registered user should have
        // no grocery-list entries yet.
        Assert.Equal(
            0,
            document.RootElement
                .GetArrayLength()
        );
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    private static object CreateRegisterRequest(
        string email,
        string password =
            "CartCompare123!")
    {
        return new
        {
            FirstName =
                "Katie",

            LastName =
                "Tester",

            Email =
                email,

            Password =
                password,

            DefaultPostalCode =
                "27606"
        };
    }
}