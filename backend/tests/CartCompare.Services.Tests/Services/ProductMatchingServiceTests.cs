using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Providers.Models;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class ProductMatchingServiceTests
{
    private readonly Mock<IRetailerProductRepository>
        _retailerProductRepositoryMock;

    private readonly ProductMatchingService
        _service;

    public ProductMatchingServiceTests()
    {
        _retailerProductRepositoryMock =
            new Mock<IRetailerProductRepository>();

        _service =
            new ProductMatchingService(
                _retailerProductRepositoryMock.Object
            );
    }

    [Fact]
    public async Task EvaluateAsync_WhenUpcMatchesExistingProduct_ReturnsUpcMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Completely Different Product",
                upc: "012345678905"
            );

        var existingProducts =
            new List<RetailerProduct>
            {
                CreateRetailerProduct(
                    itemId: 1,
                    upc: "012345678905"
                )
            };

        SetupExistingProducts(
            item.Id,
            existingProducts
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.True(result.IsMatch);

        Assert.Equal(
            ProductMatchMethod.Upc,
            result.MatchMethod
        );

        Assert.Equal(
            1.00m,
            result.MatchConfidence
        );

        Assert.Equal(
            "UPC matches an existing retailer product for this item.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenUpcFormattingDiffersButDigitsMatch_ReturnsUpcMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Whole Milk",
                upc: "012-345-678-905"
            );

        var existingProducts =
            new List<RetailerProduct>
            {
                CreateRetailerProduct(
                    itemId: 1,
                    upc: "012345678905"
                )
            };

        SetupExistingProducts(
            item.Id,
            existingProducts
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.True(result.IsMatch);

        Assert.Equal(
            ProductMatchMethod.Upc,
            result.MatchMethod
        );

        Assert.Equal(
            1.00m,
            result.MatchConfidence
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenNormalizedNamesMatchExactly_ReturnsDeterministicMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            brand: "Kroger",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Whole-Milk",
                brand: "Kroger",
                size: "1 gal"
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.True(result.IsMatch);

        Assert.Equal(
            ProductMatchMethod.Deterministic,
            result.MatchMethod
        );

        Assert.Equal(
            0.98m,
            result.MatchConfidence
        );

        Assert.Equal(
            "Normalized product names match exactly.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllMeaningfulNameTokensAreContained_ReturnsDeterministicMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Kroger Whole Milk",
                brand: "Kroger",
                size: "1 gal"
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.True(result.IsMatch);

        Assert.Equal(
            ProductMatchMethod.Deterministic,
            result.MatchMethod
        );

        Assert.Equal(
            0.90m,
            result.MatchConfidence
        );

        Assert.Equal(
            "All significant item-name terms appear in the provider product name.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenBrandDoesNotMatch_ReturnsNoMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            brand: "Kroger",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Whole Milk",
                brand: "Horizon",
                size: "1 gal"
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.False(result.IsMatch);
        Assert.Null(result.MatchMethod);
        Assert.Null(result.MatchConfidence);

        Assert.Equal(
            "The product brands do not match.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenCanonicalItemHasBrandButProviderBrandIsMissing_ReturnsNoMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            brand: "Kroger"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Whole Milk",
                brand: null
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.False(result.IsMatch);

        Assert.Equal(
            "The product brands do not match.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenSizeDoesNotMatch_ReturnsNoMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Whole Milk",
                size: "0.5 gal"
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.False(result.IsMatch);
        Assert.Null(result.MatchMethod);
        Assert.Null(result.MatchConfidence);

        Assert.Equal(
            "The product sizes do not match.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenCanonicalItemHasSizeButProviderSizeIsMissing_ReturnsNoMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Whole Milk",
                size: null
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.False(result.IsMatch);

        Assert.Equal(
            "The product sizes do not match.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenNameIsOnlyOneMeaningfulToken_DoesNotUseContainmentMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Kroger Whole Milk",
                size: "1 gal"
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.False(result.IsMatch);
        Assert.Null(result.MatchMethod);
        Assert.Null(result.MatchConfidence);

        Assert.Equal(
            "There is not enough deterministic evidence to match these products safely.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenProviderNameIsWhitespace_ReturnsNoMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "   "
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.False(result.IsMatch);
        Assert.Null(result.MatchMethod);
        Assert.Null(result.MatchConfidence);

        Assert.Equal(
            "A usable product name is missing.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenNamesAreDifferentAndContainmentFails_ReturnsNoMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Kroger Chocolate Milk",
                size: "1 gal"
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.False(result.IsMatch);
        Assert.Null(result.MatchMethod);
        Assert.Null(result.MatchConfidence);

        Assert.Equal(
            "There is not enough deterministic evidence to match these products safely.",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenSizeUsesEquivalentTextFormatting_AllowsMatch()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Large Eggs",
            size: "12 count"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Large Eggs",
                size: "12 ct"
            );

        SetupExistingProducts(
            item.Id,
            new List<RetailerProduct>()
        );

        // Act
        var result =
            await _service.EvaluateAsync(
                item,
                providerProduct
            );

        // Assert
        Assert.True(result.IsMatch);

        Assert.Equal(
            ProductMatchMethod.Deterministic,
            result.MatchMethod
        );
    }

    private void SetupExistingProducts(
        int itemId,
        List<RetailerProduct> products)
    {
        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    itemId
                )
            )
            .ReturnsAsync(products);
    }

    private static Item CreateItem(
        int id,
        string name,
        string? brand = null,
        string? size = null)
    {
        return new Item
        {
            Id = id,
            Name = name,
            Brand = brand,
            Size = size,
            IsActive = true
        };
    }

    private static ProviderProduct
        CreateProviderProduct(
            string name,
            string? brand = null,
            string? size = null,
            string? upc = null)
    {
        return new ProviderProduct
        {
            ExternalProductId =
                "TEST-PRODUCT",

            Name =
                name,

            Brand =
                brand,

            Size =
                size,

            Upc =
                upc
        };
    }

    private static RetailerProduct
        CreateRetailerProduct(
            int itemId,
            string? upc = null)
    {
        return new RetailerProduct
        {
            Id = 1,

            ItemId =
                itemId,

            RetailerId =
                1,

            ExternalProductId =
                "EXISTING-PRODUCT",

            Name =
                "Existing Product",

            Upc =
                upc,

            MatchMethod =
                ProductMatchMethod.Manual,

            IsActive =
                true
        };
    }
}