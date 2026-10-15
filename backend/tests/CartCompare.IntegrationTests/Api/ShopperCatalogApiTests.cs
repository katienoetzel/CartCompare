using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Providers.Models;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Api;

// Shoppers add items through api/catalog by choosing an option the
// server builds from a real product; they cannot supply a name.
public class ShopperCatalogApiTests
    : PostgresIntegrationTestBase
{
    private static readonly ProviderProduct Doritos = new()
    {
        ExternalProductId = "doritos",
        Name = "Doritos® Nacho Cheese Flavored Tortilla Chips",
        Brand = "Doritos",
        Size = "9.25 oz",
        ShelfPrice = 5.49m
    };

    [Fact]
    public async Task Search_NeedsNoLocationAndHidesPrices()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var storeId = await SeedStoreAsync();
        await SignInAsync(client, "search-shopper@example.com");

        var response = await client.GetAsync("/api/catalog/products?query=doritos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var retailer = Assert.Single(document.RootElement.GetProperty("retailers").EnumerateArray());
        Assert.Equal("Harris Teeter", retailer.GetProperty("retailerName").GetString());
        Assert.Equal(storeId, retailer.GetProperty("storeLocationId").GetInt32());
        var product = Assert.Single(retailer.GetProperty("products").EnumerateArray());
        Assert.Equal("doritos", product.GetProperty("externalProductId").GetString());
        Assert.False(product.TryGetProperty("shelfPrice", out _));
    }

    [Fact]
    public async Task Options_ListTheBrandedAndGenericItems()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var storeId = await SeedStoreAsync();
        await SignInAsync(client, "options-shopper@example.com");

        var response = await client.GetAsync(
            $"/api/catalog/products/doritos/options?storeLocationId={storeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var newItems = document.RootElement.GetProperty("newItems");
        Assert.Equal(2, newItems.GetArrayLength());
        Assert.Equal("Branded", newItems[0].GetProperty("kind").GetString());
        Assert.Equal("Doritos", newItems[0].GetProperty("brand").GetString());
        Assert.Equal("Generic", newItems[1].GetProperty("kind").GetString());
        Assert.Empty(document.RootElement.GetProperty("existingItems").EnumerateArray());
    }

    [Fact]
    public async Task CreateItem_IgnoresAnySuppliedNameAndRecordsTheShopper()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var storeId = await SeedStoreAsync();
        var userId = await SignInAsync(client, "creating-shopper@example.com");

        // "name" is not part of the request; a bad actor's text is dropped.
        var response = await client.PostAsJsonAsync("/api/catalog/items", new
        {
            storeLocationId = storeId,
            externalProductId = "doritos",
            kind = "Branded",
            name = "Cheese Chips"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("created").GetBoolean());
        var item = document.RootElement.GetProperty("item");
        Assert.Equal("Nacho Cheese Flavored Tortilla Chips", item.GetProperty("name").GetString());
        Assert.Equal("Doritos", item.GetProperty("brand").GetString());
        Assert.False(item.TryGetProperty("createdByUserId", out _));

        await using var context = CreateDbContext();
        var saved = await context.Set<Item>().SingleAsync();
        Assert.Equal(userId, saved.CreatedByUserId);
    }

    [Fact]
    public async Task CreateItem_RequiresSignIn()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/catalog/items", new
        {
            storeLocationId = 1,
            externalProductId = "doritos",
            kind = "Generic"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private CartCompareWebApplicationFactory CreateFactory()
    {
        var factory = new CartCompareWebApplicationFactory(ConnectionString);
        factory.TestPriceProvider.SupportedRetailers.Add("Harris Teeter");
        factory.TestPriceProvider.ProductsToReturn.Add(Doritos);
        return factory;
    }

    private async Task<int> SeedStoreAsync()
    {
        await using var context = CreateDbContext();

        var retailer = new Retailer { Name = "Harris Teeter", IsActive = true, CreatedAt = DateTime.UtcNow };
        context.Set<Retailer>().Add(retailer);
        await context.SaveChangesAsync();

        var store = new StoreLocation
        {
            RetailerId = retailer.Id, ExternalLocationId = "09700304", Name = "Test Store",
            AddressLine1 = "4221 Corners Pkwy", City = "Raleigh", State = "NC", PostalCode = "27617",
            IsActive = true, LastSeenAt = DateTime.UtcNow
        };
        context.Set<StoreLocation>().Add(store);
        await context.SaveChangesAsync();

        return store.Id;
    }

    private static async Task<int> SignInAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            FirstName = "Katie",
            LastName = "Tester",
            Email = email,
            Password = "CartCompare123!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", document.RootElement.GetProperty("token").GetString());

        return document.RootElement.GetProperty("userId").GetInt32();
    }
}
