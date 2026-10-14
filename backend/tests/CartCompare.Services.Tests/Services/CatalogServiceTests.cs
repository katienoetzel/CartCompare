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
    private readonly CatalogService _service;

    private readonly ProviderProduct _peanutButter = new()
    {
        ExternalProductId = "pb", Name = "Harris Teeter™ Creamy Peanut Butter 16 oz",
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
        _provider.Setup(provider => provider.GetProductAsync("Harris Teeter", "store", "pb"))
            .ReturnsAsync(_peanutButter);
        _items.Setup(repository => repository.GetActiveAsync()).ReturnsAsync(new List<Item>());

        var retailerProducts = new Mock<IRetailerProductRepository>();
        retailerProducts.Setup(repository => repository.GetByItemIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<RetailerProduct>());

        _service = new CatalogService(
            _items.Object, _stores.Object, _retailers.Object, _providers.Object,
            new ProductMatchingService(retailerProducts.Object));
    }

    [Theory]
    [InlineData("Harris Teeter™ Creamy Peanut Butter 16 oz", "Harris Teeter", "Creamy Peanut Butter")]
    [InlineData("Kellogg's Corn Flakes Cereal Large Size 12 oz", null, "Kellogg's Corn Flakes Cereal Large Size")]
    [InlineData("Banana (Bananas are sold individually) (Avg. 0.4lb)", null, "Banana")]
    [InlineData("PERDUE® Fresh Boneless Skinless Chicken Breasts", "Perdue", "Fresh Boneless Skinless Chicken Breasts")]
    [InlineData("Lowes Foods Creamy Peanut Butter 16 oz", null, "Creamy Peanut Butter")]
    public void SuggestName_DropsBrandSizeAndTrademarks(string name, string? brand, string expected)
    {
        var product = new ProviderProduct { ExternalProductId = "x", Name = name, Brand = brand };
        Assert.Equal(expected, CatalogService.SuggestName(product, "Lowes Foods"));
    }

    [Fact]
    public async Task SearchAsync_ReturnsSizedProductsWithSuggestedNames()
    {
        _provider.Setup(provider => provider.SearchProductsAsync("Harris Teeter", "store", "peanut butter"))
            .ReturnsAsync(new List<ProviderProduct>
            {
                _peanutButter,
                new() { ExternalProductId = "nosize", Name = "Peanut Butter Sampler" }
            });

        var result = await _service.SearchAsync(2, " peanut butter ");

        Assert.Equal(CatalogResultType.Succeeded, result.Result);
        var product = Assert.Single(result.Products);
        Assert.Equal("Creamy Peanut Butter", product.SuggestedName);
        Assert.Equal(2.49m, product.ShelfPrice);
    }

    [Fact]
    public async Task CreateItemFromProductAsync_CreatesAGenericItemThatMatchesTheProduct()
    {
        var result = await _service.CreateItemFromProductAsync(2, "pb", " Creamy  Peanut Butter ", "1 lb", "Pantry");

        Assert.Equal(CatalogResultType.Succeeded, result.Result);
        Assert.True(result.Created);
        Assert.Equal("Creamy Peanut Butter", result.Item!.Name);
        Assert.Null(result.Item.Brand);
        Assert.Equal("1 lb", result.Item.Size);
        Assert.Equal("Pantry", result.Item.Category);
        _items.Verify(repository => repository.AddAsync(result.Item), Times.Once);
    }

    [Theory]
    [InlineData("Crunchy Peanut Butter", "16 oz")]
    [InlineData("Creamy Peanut Butter", "40 oz")]
    [InlineData("Almond Butter", "16 oz")]
    public async Task CreateItemFromProductAsync_RejectsANameOrSizeThePickedProductDoesNotMatch(
        string name, string size)
    {
        var result = await _service.CreateItemFromProductAsync(2, "pb", name, size, null);

        Assert.Equal(CatalogResultType.NameDoesNotMatchProduct, result.Result);
        _items.Verify(repository => repository.AddAsync(It.IsAny<Item>()), Times.Never);
    }

    [Fact]
    public async Task CreateItemFromProductAsync_ReusesAnEquivalentExistingItem()
    {
        var existing = new Item { Id = 9, Name = "creamy peanut butter", Size = "16 oz", IsActive = true };
        _items.Setup(repository => repository.GetActiveAsync()).ReturnsAsync(new List<Item> { existing });

        var result = await _service.CreateItemFromProductAsync(2, "pb", "Creamy Peanut Butter", "1 lb", null);

        Assert.Equal(CatalogResultType.Succeeded, result.Result);
        Assert.False(result.Created);
        Assert.Same(existing, result.Item);
        _items.Verify(repository => repository.AddAsync(It.IsAny<Item>()), Times.Never);
    }

    [Fact]
    public async Task CreateItemFromProductAsync_WhenStoreNoLongerListsProduct_ReturnsProductNotFound()
    {
        _provider.Setup(provider => provider.GetProductAsync("Harris Teeter", "store", "gone"))
            .ReturnsAsync((ProviderProduct?)null);

        var result = await _service.CreateItemFromProductAsync(2, "gone", "Creamy Peanut Butter", "16 oz", null);

        Assert.Equal(CatalogResultType.ProductNotFound, result.Result);
    }
}
