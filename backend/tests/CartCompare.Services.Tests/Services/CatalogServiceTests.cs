using CartCompare.Entities;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class CatalogServiceTests
{
    private readonly Mock<IItemRepository> _items = new();
    private readonly Mock<IStoreLocationRepository> _stores = new();
    private readonly Mock<IRetailerRepository> _retailers = new();
    private readonly Mock<IPriceProviderResolver> _providers = new();
    private readonly Mock<IPriceProvider> _provider = new();
    private readonly List<Item> _catalog = new();
    private readonly CatalogService _service;

    private static readonly ProviderProduct Doritos = new()
    {
        ExternalProductId = "doritos", Name = "Doritos® Nacho Cheese Flavored Tortilla Chips",
        Brand = "Doritos", Size = "9.25 oz", ShelfPrice = 5.49m
    };

    private static readonly ProviderProduct StoreBrandPeanutButter = new()
    {
        ExternalProductId = "ht-pb", Name = "Harris Teeter™ Creamy Peanut Butter",
        Brand = "Harris Teeter", Size = "16 oz", ShelfPrice = 2.49m
    };

    public CatalogServiceTests()
    {
        var store = new StoreLocation
        {
            Id = 2, RetailerId = 3, ExternalLocationId = "store", AddressLine1 = "Main St",
            City = "Raleigh", State = "NC", PostalCode = "27613"
        };
        _stores.Setup(repository => repository.GetByIdAsync(2)).ReturnsAsync(store);
        _retailers.Setup(repository => repository.GetByIdAsync(3))
            .ReturnsAsync(new Retailer { Id = 3, Name = "Harris Teeter" });
        _providers.Setup(resolver => resolver.Resolve("Harris Teeter")).Returns(_provider.Object);
        foreach (var product in new[] { Doritos, StoreBrandPeanutButter })
        {
            _provider.Setup(provider => provider.GetProductAsync("Harris Teeter", "store", product.ExternalProductId))
                .ReturnsAsync(product);
        }
        _items.Setup(repository => repository.GetActiveAsync()).ReturnsAsync(_catalog);

        _service = new CatalogService(
            _items.Object, _stores.Object, _retailers.Object, _providers.Object,
            new ProductMatchingService(new Mock<IRetailerProductRepository>().Object));
    }

    [Theory]
    [InlineData("Harris Teeter™ Creamy Peanut Butter 16 oz", "Harris Teeter", "Creamy Peanut Butter")]
    [InlineData("Doritos® Nacho Cheese Flavored Tortilla Chips", "Doritos", "Nacho Cheese Flavored Tortilla Chips")]
    [InlineData("Lowes Foods Creamy Peanut Butter 16 oz", null, "Creamy Peanut Butter")]
    [InlineData("Banana (Bananas are sold individually) (Avg. 0.4lb)", null, "Banana")]
    public void BaseName_DropsBrandSizeAndTrademarks(string name, string? brand, string expected)
    {
        var product = new ProviderProduct { ExternalProductId = "x", Name = name, Brand = brand };
        Assert.Equal(expected, CatalogService.BaseName(product, "Lowes Foods"));
    }

    [Fact]
    public async Task SearchAsync_ReturnsOnlySizedProducts()
    {
        _provider.Setup(provider => provider.SearchProductsAsync("Harris Teeter", "store", "doritos"))
            .ReturnsAsync(new List<ProviderProduct>
            {
                Doritos,
                new() { ExternalProductId = "nosize", Name = "Doritos Variety Pack" }
            });

        var result = await _service.SearchAsync(2, " doritos ");

        Assert.Equal(CatalogResultType.Succeeded, result.Result);
        Assert.Equal("doritos", Assert.Single(result.Products).ExternalProductId);
    }

    [Fact]
    public async Task GetOptionsAsync_OffersTheBrandAndAnyBrand()
    {
        var result = await _service.GetOptionsAsync(2, "doritos");

        Assert.Equal(CatalogResultType.Succeeded, result.Result);
        Assert.Empty(result.ExistingItems);
        Assert.Collection(result.NewItems,
            branded =>
            {
                Assert.Equal(CatalogItemKind.Branded, branded.Kind);
                Assert.Equal("Nacho Cheese Flavored Tortilla Chips", branded.Name);
                Assert.Equal("Doritos", branded.Brand);
                Assert.Equal("9.25 oz", branded.Size);
            },
            generic =>
            {
                Assert.Equal(CatalogItemKind.Generic, generic.Kind);
                Assert.Equal("Nacho Cheese Flavored Tortilla Chips", generic.Name);
                Assert.Null(generic.Brand);
            });
    }

    [Fact]
    public async Task GetOptionsAsync_ListsExistingItemsTheProductAlreadyCountsAs()
    {
        // The duplicate trap: a generic item already covers this bag.
        var generic = new Item { Id = 7, Name = "Nacho Cheese Tortilla Chips", Size = "9.25 oz", IsActive = true };
        var otherBrand = new Item { Id = 8, Name = "Nacho Cheese Tortilla Chips", Brand = "Tostitos", Size = "9.25 oz", IsActive = true };
        _catalog.AddRange(new[] { generic, otherBrand });

        var result = await _service.GetOptionsAsync(2, "doritos");

        Assert.Same(generic, Assert.Single(result.ExistingItems));
        Assert.Equal(2, result.NewItems.Count);
    }

    [Fact]
    public async Task GetOptionsAsync_DoesNotOfferAnOptionThatAlreadyExists()
    {
        _catalog.Add(new Item
        {
            Id = 9, Name = "Nacho Cheese Flavored Tortilla Chips", Brand = "Doritos", Size = "9.25 oz", IsActive = true
        });

        var result = await _service.GetOptionsAsync(2, "doritos");

        Assert.Equal(9, Assert.Single(result.ExistingItems).Id);
        Assert.Equal(CatalogItemKind.Generic, Assert.Single(result.NewItems).Kind);
    }

    [Fact]
    public async Task GetOptionsAsync_StoreBrandsOnlyBecomeGenericItems()
    {
        var result = await _service.GetOptionsAsync(2, "ht-pb");

        var option = Assert.Single(result.NewItems);
        Assert.Equal(CatalogItemKind.Generic, option.Kind);
        Assert.Equal("Creamy Peanut Butter", option.Name);
        Assert.Null(option.Brand);
    }

    [Fact]
    public async Task CreateItemAsync_BuildsTheItemFromTheProductAndRecordsTheShopper()
    {
        var result = await _service.CreateItemAsync(2, "doritos", CatalogItemKind.Branded, createdByUserId: 42);

        Assert.Equal(CatalogResultType.Succeeded, result.Result);
        Assert.True(result.Created);
        Assert.Equal("Nacho Cheese Flavored Tortilla Chips", result.Item!.Name);
        Assert.Equal("Doritos", result.Item.Brand);
        Assert.Equal("9.25 oz", result.Item.Size);
        Assert.Equal(42, result.Item.CreatedByUserId);
        _items.Verify(repository => repository.AddAsync(result.Item), Times.Once);
    }

    [Fact]
    public async Task CreateItemAsync_ReusesAnEquivalentItem()
    {
        var existing = new Item { Id = 9, Name = "nacho cheese flavored tortilla chips", Size = "9.25 ounces", IsActive = true };
        _catalog.Add(existing);

        var result = await _service.CreateItemAsync(2, "doritos", CatalogItemKind.Generic, createdByUserId: 42);

        Assert.False(result.Created);
        Assert.Same(existing, result.Item);
        _items.Verify(repository => repository.AddAsync(It.IsAny<Item>()), Times.Never);
    }

    [Fact]
    public async Task CreateItemAsync_RefusesABrandedStoreBrandItem()
    {
        var result = await _service.CreateItemAsync(2, "ht-pb", CatalogItemKind.Branded, createdByUserId: 42);

        Assert.Equal(CatalogResultType.OptionNotAvailable, result.Result);
        _items.Verify(repository => repository.AddAsync(It.IsAny<Item>()), Times.Never);
    }

    [Fact]
    public async Task Options_ThatWouldNotMatchTheirOwnProduct_AreDropped()
    {
        // A one-word name is too vague to match automatically unless sold per lb.
        _provider.Setup(provider => provider.GetProductAsync("Harris Teeter", "store", "chips"))
            .ReturnsAsync(new ProviderProduct
            {
                ExternalProductId = "chips", Name = "Pringles® Chips", Brand = "Pringles", Size = "5.5 oz"
            });

        var result = await _service.GetOptionsAsync(2, "chips");

        Assert.Empty(result.NewItems);
    }

    [Fact]
    public async Task GetOptionsAsync_WhenTheStoreNoLongerListsTheProduct_ReturnsProductNotFound()
    {
        _provider.Setup(provider => provider.GetProductAsync("Harris Teeter", "store", "gone"))
            .ReturnsAsync((ProviderProduct?)null);

        var result = await _service.GetOptionsAsync(2, "gone");

        Assert.Equal(CatalogResultType.ProductNotFound, result.Result);
    }
}
