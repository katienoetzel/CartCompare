using System.Net;
using System.Net.Http.Json;
using CartCompare.Infrastructure.Providers;
using CartCompare.Infrastructure.Providers.Kroger;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Providers.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CartCompare.IntegrationTests.Providers;

public class HarrisTeeterProviderTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task FindStores_UsesHarrisTeeterChainAndExcludesOtherChains()
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
                        expires_in = 1800,
                        token_type = "bearer"
                    })
                };
            }

            if (request.Method == HttpMethod.Get &&
                request.PathAndQuery.StartsWith(
                    "/v1/locations?",
                    StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        data = new object[]
                        {
                            new
                            {
                                locationId = "HT-90",
                                chain = "HART",
                                name = "Leesville Towne Centre",
                                address = new
                                {
                                    addressLine1 = "13210 Strickland Road",
                                    city = "Raleigh",
                                    state = "NC",
                                    zipCode = "27613"
                                }
                            },
                            new
                            {
                                locationId = "K-1",
                                chain = "Kroger",
                                name = "Other Chain",
                                address = new
                                {
                                    addressLine1 = "100 Main Street",
                                    city = "Raleigh",
                                    state = "NC",
                                    zipCode = "27613"
                                }
                            }
                        }
                    })
                };
            }

            throw new InvalidOperationException(
                $"Unexpected request: {request.PathAndQuery}"
            );
        };

        using var scope = factory.Services.CreateScope();
        var krogerProvider = scope.ServiceProvider
            .GetRequiredService<KrogerPriceProvider>();
        var resolver = new PriceProviderResolver(
            new IPriceProvider[] { krogerProvider }
        );
        var provider = resolver.Resolve("Harris Teeter");

        Assert.IsType<KrogerPriceProvider>(provider);
        Assert.True(provider!.SupportsRetailer("harris teeter"));
        Assert.False(provider.SupportsRetailer("Other Chain"));

        var stores = await provider.FindStoresAsync(
            "Harris Teeter", "27613"
        );

        var store = Assert.Single(stores);
        Assert.Equal("HT-90", store.ExternalLocationId);
        Assert.Equal("27613", store.PostalCode);

        var request = Assert.Single(
            factory.TestKrogerHttpHandler.Requests,
            captured => captured.Method == HttpMethod.Get
        );
        Assert.Contains(
            "filter.zipCode.near=27613",
            request.PathAndQuery
        );
        Assert.Contains(
            "filter.chain=HART",
            request.PathAndQuery
        );
    }
}
