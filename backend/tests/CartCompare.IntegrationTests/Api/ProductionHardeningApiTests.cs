using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CartCompare.IntegrationTests.Api;

public class ProductionHardeningApiTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task Login_BeyondTheLimit_ReturnsTooManyRequests()
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString,
                settings: new Dictionary<string, string?>
                {
                    ["RateLimits:AuthPerMinute"] = "2"
                }
            );

        using var client = factory.CreateClient();
        var login = new { Email = "nobody@example.com", Password = "Wrong123!" };

        // Act
        var first = await client.PostAsJsonAsync("/api/auth/login", login);
        var second = await client.PostAsJsonAsync("/api/auth/login", login);
        var third = await client.PostAsJsonAsync("/api/auth/login", login);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);

        using var document = JsonDocument.Parse(await third.Content.ReadAsStringAsync());
        Assert.Equal(
            "Too many requests. Wait a minute and try again.",
            document.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(true, HttpStatusCode.Unauthorized)]
    [InlineData(false, HttpStatusCode.TooManyRequests)]
    public async Task LoginLimit_BehindAProxy_IsPerForwardedClientAddress(
        bool trustForwardedHeaders, HttpStatusCode secondClientStatus)
    {
        // Arrange
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString,
                settings: new Dictionary<string, string?>
                {
                    ["RateLimits:AuthPerMinute"] = "1",
                    ["ForwardedHeaders:Enabled"] = trustForwardedHeaders.ToString()
                }
            );

        using var client = factory.CreateClient();

        // Act: two shoppers behind the same proxy.
        var first = await LoginFromAsync(client, "203.0.113.10");
        var second = await LoginFromAsync(client, "203.0.113.20");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(secondClientStatus, second.StatusCode);
    }

    private static Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string clientAddress)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { Email = "nobody@example.com", Password = "Wrong123!" })
        };
        request.Headers.Add("X-Forwarded-For", clientAddress);
        return client.SendAsync(request);
    }

    [Fact]
    public void Startup_OutsideDevelopmentWithoutFrontendOrigin_Fails()
    {
        using var factory = new ProductionFactory(ConnectionString, frontendOrigin: null);

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Equal("Frontend:Origin must be configured outside Development.", error.Message);
    }

    [Theory]
    [InlineData("/api/dev/lowes-foods/stores?postalCode=27613")]
    [InlineData("/api/dev/kroger/nearby-chains?postalCode=27613")]
    public async Task DevelopmentControllers_OutsideDevelopment_AreNotRouted(string path)
    {
        // Arrange
        using var factory = new ProductionFactory(ConnectionString);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Not merely a 404 from the action's own check: no route exists.
        var routes = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? "");
        Assert.DoesNotContain(routes, route => route.StartsWith("api/dev/"));
        Assert.Contains(routes, route => route.StartsWith("api/comparisons"));
    }

    private sealed class ProductionFactory(
        string connectionString,
        string? frontendOrigin = "https://cartcompare.example")
        : CartCompareWebApplicationFactory(connectionString)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");

            // Production loads no user secrets, and Program reads these
            // before the factory's test configuration is applied.
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("Jwt:Key", "ProductionHardeningTestsOnlyJwtKey_2026_NotARealSecret_123456789");
            builder.UseSetting("Jwt:Issuer", "CartCompare.IntegrationTests");
            builder.UseSetting("Jwt:Audience", "CartCompare.IntegrationTests");
            if (frontendOrigin is not null)
            {
                builder.UseSetting("Frontend:Origin", frontendOrigin);
            }
        }
    }
}
