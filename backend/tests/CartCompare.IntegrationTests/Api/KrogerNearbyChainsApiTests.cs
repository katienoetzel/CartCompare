using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.IntegrationTests.Fixtures;

namespace CartCompare.IntegrationTests.Api;

public class KrogerNearbyChainsApiTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task NearbyChains_ReturnsChainCountsWithoutFilteringOrSaving()
    {
        using var factory = new CartCompareWebApplicationFactory(
            ConnectionString
        );
        factory.TestKrogerHttpHandler.ResponseFactory = request =>
        {
            if (request.Method == HttpMethod.Post &&
                request.PathAndQuery == "/v1/connect/oauth2/token")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "fake-token",
                        expires_in = 1800
                    })
                };
            }

            if (request.Method == HttpMethod.Get &&
                request.PathAndQuery.StartsWith("/v1/locations?",
                    StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        data = new[]
                        {
                            new { chain = "Harris Teeter" },
                            new { chain = "Harris Teeter" },
                            new { chain = "Kroger" }
                        }
                    })
                };
            }

            throw new InvalidOperationException(
                $"Unexpected request: {request.PathAndQuery}"
            );
        };

        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/dev/kroger/nearby-chains?postalCode=27613"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );
        Assert.Equal(3, document.RootElement
            .GetProperty("locationsReturned").GetInt32());
        var chains = document.RootElement.GetProperty("chains");
        Assert.Equal("Harris Teeter", chains[0].GetProperty("name").GetString());
        Assert.Equal(2, chains[0].GetProperty("count").GetInt32());

        var locationRequest = Assert.Single(
            factory.TestKrogerHttpHandler.Requests,
            request => request.Method == HttpMethod.Get
        );
        Assert.Contains("filter.zipCode.near=27613",
            locationRequest.PathAndQuery);
        Assert.DoesNotContain("filter.chain=",
            locationRequest.PathAndQuery);

        await using var context = CreateDbContext();
        Assert.Empty(context.StoreLocations);
    }

    [Fact]
    public async Task NearbyChains_RejectsInvalidPostalCodeBeforeProviderCall()
    {
        using var factory = new CartCompareWebApplicationFactory(
            ConnectionString
        );
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/dev/kroger/nearby-chains?postalCode=bad"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.TestKrogerHttpHandler.Requests);
    }
}
