using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.Infrastructure.Providers.Kroger;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Providers.Models;

namespace CartCompare.IntegrationTests.Api;

public class NearbyStoreSearchApiTests : PostgresIntegrationTestBase
{
    private const string SearchUrl = "/api/store-locations/search";

    [Fact]
    public async Task Search_RequiresAuthentication()
    {
        using var factory = new CartCompareWebApplicationFactory(
            ConnectionString,
            strictAdminAuthorization: true
        );
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            SearchUrl,
            new { postalCode = "27613" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("2761")]
    [InlineData("27613-1234")]
    [InlineData("abcde")]
    public async Task Search_RejectsInvalidPostalCode(string postalCode)
    {
        using var factory = new CartCompareWebApplicationFactory(
            ConnectionString
        );
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        using var response = await client.PostAsJsonAsync(
            SearchUrl,
            new { postalCode }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.TestPriceProvider.FindStoresCalls);
    }

    [Fact]
    public async Task Search_ReturnsCurrentStoresAcrossRetailersWithStableIds()
    {
        var krogerId = await SeedRetailerAsync("Kroger");
        var lowesId = await SeedRetailerAsync("Lowes Foods");
        await SeedRetailerAsync("Unsupported Market");

        using var factory = new CartCompareWebApplicationFactory(
            ConnectionString,
            strictAdminAuthorization: true
        );
        factory.TestPriceProvider.SupportedRetailers.Add("Kroger");
        factory.TestPriceProvider.SupportedRetailers.Add("Lowes Foods");
        factory.TestPriceProvider.FindStoresHandler = (retailer, postalCode) =>
            Task.FromResult(new List<ProviderStoreLocation>
            {
                MakeStore(
                    retailer == "Kroger" ? "K-1" : "L-1",
                    retailer == "Kroger" ? "Kroger Raleigh" : "Lowes Raleigh"
                )
            });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        using var firstResponse = await client.PostAsJsonAsync(
            SearchUrl,
            new { postalCode = " 27613 " }
        );
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        using var first = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync()
        );
        var firstStores = first.RootElement.GetProperty("stores");
        Assert.Equal(2, firstStores.GetArrayLength());
        Assert.Empty(first.RootElement.GetProperty("failedRetailers").EnumerateArray());
        Assert.Equal("27613", first.RootElement.GetProperty("postalCode").GetString());
        Assert.Contains(firstStores.EnumerateArray(), store =>
            store.GetProperty("retailerId").GetInt32() == krogerId &&
            store.GetProperty("externalLocationId").GetString() == "K-1");
        Assert.Contains(firstStores.EnumerateArray(), store =>
            store.GetProperty("retailerId").GetInt32() == lowesId &&
            store.GetProperty("externalLocationId").GetString() == "L-1");
        var ids = firstStores.EnumerateArray()
            .Select(store => store.GetProperty("id").GetInt32())
            .OrderBy(id => id).ToArray();

        // A new search only returns locations in that provider response.
        factory.TestPriceProvider.FindStoresHandler = (retailer, postalCode) =>
            Task.FromResult(retailer == "Kroger"
                ? new List<ProviderStoreLocation>()
                : new List<ProviderStoreLocation> { MakeStore("L-1", "Lowes Raleigh") });

        using var secondResponse = await client.PostAsJsonAsync(
            SearchUrl,
            new { postalCode = "27613" }
        );
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        using var second = JsonDocument.Parse(
            await secondResponse.Content.ReadAsStringAsync()
        );
        var secondStores = second.RootElement.GetProperty("stores");
        Assert.Equal(1, secondStores.GetArrayLength());
        Assert.Equal(lowesId, secondStores[0].GetProperty("retailerId").GetInt32());
        Assert.Contains(secondStores[0].GetProperty("id").GetInt32(), ids);
        Assert.Equal(4, factory.TestPriceProvider.FindStoresCalls.Count);
        Assert.All(factory.TestPriceProvider.FindStoresCalls, call =>
            Assert.Equal("27613", call.PostalCode));
    }

    [Fact]
    public async Task Search_ReportsOneProviderFailureAndReturnsOtherStores()
    {
        await SeedRetailerAsync("Kroger");
        await SeedRetailerAsync("Lowes Foods");

        using var factory = new CartCompareWebApplicationFactory(
            ConnectionString
        );
        factory.TestPriceProvider.SupportedRetailers.Add("Kroger");
        factory.TestPriceProvider.SupportedRetailers.Add("Lowes Foods");
        factory.TestPriceProvider.FindStoresHandler = (retailer, postalCode) =>
            retailer == "Kroger"
                ? Task.FromException<List<ProviderStoreLocation>>(
                    new KrogerApiException("locations", HttpStatusCode.BadGateway))
                : Task.FromResult(new List<ProviderStoreLocation>
                {
                    MakeStore("L-1", "Lowes Raleigh")
                });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var response = await client.PostAsJsonAsync(
            SearchUrl,
            new { postalCode = "27613" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );
        Assert.Equal(1, document.RootElement.GetProperty("stores").GetArrayLength());
        var failed = document.RootElement.GetProperty("failedRetailers");
        Assert.Equal(1, failed.GetArrayLength());
        Assert.Equal("Kroger", failed[0].GetProperty("name").GetString());
        Assert.Equal("http_502", failed[0].GetProperty("reason").GetString());
        Assert.Equal("locations", failed[0].GetProperty("stage").GetString());
    }

    private static ProviderStoreLocation MakeStore(string externalId, string name) =>
        new()
        {
            ExternalLocationId = externalId,
            Name = name,
            AddressLine1 = "100 Main Street",
            City = "Raleigh",
            State = "NC",
            PostalCode = "27615"
        };

    private async Task<int> SeedRetailerAsync(string name)
    {
        await using var context = CreateDbContext();
        var retailer = new Retailer
        {
            Name = name,
            SupportsMembership = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Retailers.Add(retailer);
        await context.SaveChangesAsync();
        return retailer.Id;
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "Katie",
                lastName = "Tester",
                email = $"nearby-{Guid.NewGuid():N}@example.com",
                password = "CartCompare123!",
                defaultPostalCode = "27613"
            }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var auth = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth.RootElement.GetProperty("token").GetString()
            );
    }
}
