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

    [Theory]
    [InlineData("Harris Teeter Vitamin D Milk", "Harris Teeter")]
    [InlineData("Kroger® Vitamin D Whole Milk", "Kroger")]
    [InlineData("Lowes Foods Vitamin D Whole Milk 1 gal", "Lowes Foods")]
    public async Task EvaluateAsync_WhenGenericItemMatchesAnyBrandsWholeMilk_ReturnsDeterministicMatch(
        string providerName,
        string providerBrand)
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: providerName,
                brand: providerBrand,
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
    }

    [Theory]
    [InlineData("Kroger Chocolate Whole Milk", "chocolate")]
    [InlineData("Harris Teeter Lactose Free Whole Milk", "lactose")]
    [InlineData("Simple Truth Organic Whole Milk", "organic")]
    [InlineData("Fairlife Ultra-Filtered Whole Milk", "ultra")]
    public async Task EvaluateAsync_WhenProviderProductIsDifferentVariant_ReturnsNoMatch(
        string providerName,
        string expectedVariant)
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: providerName,
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

        Assert.Equal(
            $"The provider product is a different variant ({expectedVariant}).",
            result.Reason
        );
    }

    [Theory]
    [InlineData("Harris Teeter 2% Reduced Fat Vitamin D Milk")]
    [InlineData("Kroger 1 % Lowfat Vitamin D Milk")]
    public async Task EvaluateAsync_WhenProviderProductHasFatPercentage_ReturnsNoMatch(
        string providerName)
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Whole Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: providerName,
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

        Assert.Equal(
            "The provider product is a different variant (fat percentage).",
            result.Reason
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenItemNameIncludesVariant_AllowsThatVariant()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "Chocolate Milk",
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
        Assert.True(result.IsMatch);
    }

    [Fact]
    public async Task EvaluateAsync_WhenItemNameIncludesFatPercentage_AllowsThatPercentage()
    {
        // Arrange
        var item = CreateItem(
            id: 1,
            name: "2% Reduced Fat Milk",
            size: "1 gallon"
        );

        var providerProduct =
            CreateProviderProduct(
                name: "Harris Teeter 2% Reduced Fat Milk",
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

    // Real products from the first basket run (Lowe's Foods and Harris Teeter, 2026-10-13).
    [Theory]
    [InlineData("Salted Butter", "1 lb", "Highland Crest Salted Butter", "16 oz")]
    [InlineData("Salted Butter", "1 lb", "Harris Teeter™ Salted Butter Quarters", "4 ct / 16 oz")]
    [InlineData("Long Grain White Rice", "2 lb", "Lowes Foods Long Grain White Rice 32 oz", "32 oz")]
    [InlineData("Whole Milk", "1 gallon", "Harris Teeter Whole Milk", "128 fl oz")]
    [InlineData("Whole Milk", "1/2 gallon", "Harris Teeter® Vitamin D Whole Milk", "1/2 gal")]
    [InlineData("Large Eggs", "12 count", "Lowes Foods Grade A Large White Dozen Eggs", "12 ct")]
    [InlineData("Large Eggs", "1 dozen", "Harris Teeter Large Grade A White Eggs", "12 ct")]
    [InlineData("Ground Coffee", "12 oz", "Dunkin’ Original Blend Medium Roast Ground Coffee", "12 ounces")]
    [InlineData("Corn Flakes Cereal", "12 oz", "Kellogg's Corn Flakes Cereal Large Size 12 oz", "12 oz")]
    [InlineData("Boneless Skinless Chicken Breast", "1 lb", "PERDUE® Fresh Boneless Skinless Chicken Breasts", "1 lb")]
    [InlineData("Boneless Skinless Chicken Breast", "1 lb", "Just Bare Boneless & Skinless Chicken Breast Fillets", "16 oz")]
    public async Task EvaluateAsync_WhenSizesAreEquivalentAndNameWordsVary_ReturnsDeterministicMatch(
        string itemName, string itemSize, string productName, string productSize)
    {
        // Arrange
        var item = CreateItem(id: 1, name: itemName, size: itemSize);
        var providerProduct = CreateProviderProduct(name: productName, size: productSize);
        SetupExistingProducts(item.Id, new List<RetailerProduct>());

        // Act
        var result = await _service.EvaluateAsync(item, providerProduct);

        // Assert
        Assert.True(result.IsMatch, result.Reason);
        Assert.Equal(ProductMatchMethod.Deterministic, result.MatchMethod);
    }

    [Theory]
    [InlineData("Salted Butter", "1 lb", "Challenge® Salted Butter Sticks", "8 oz")]
    [InlineData("Whole Milk", "1 gallon", "Harris Teeter® Vitamin D Whole Milk", "1/2 gal")]
    [InlineData("Whole Milk", "1 gallon", "Harris Teeter Whole Milk", "1 lb")]
    [InlineData("Large Eggs", "12 count", "Large Eggs", "18 ct")]
    public async Task EvaluateAsync_WhenAmountsOrDimensionsDiffer_ReturnsNoMatch(
        string itemName, string itemSize, string productName, string productSize)
    {
        // Arrange
        var item = CreateItem(id: 1, name: itemName, size: itemSize);
        var providerProduct = CreateProviderProduct(name: productName, size: productSize);
        SetupExistingProducts(item.Id, new List<RetailerProduct>());

        // Act
        var result = await _service.EvaluateAsync(item, providerProduct);

        // Assert
        Assert.False(result.IsMatch);
        Assert.Equal("The product sizes do not match.", result.Reason);
    }

    [Theory]
    [InlineData("Large Eggs", "12 ct", "Eggland's Best Classic Extra Large White Eggs, 12 Count", "extra")]
    [InlineData("Salted Butter", "1 lb", "Breakstone's Kosher Salted Whipped Butter", "whipped")]
    [InlineData("Ground Coffee", "12 oz", "Folgers Decaf Classic Roast Ground Coffee", "decaf")]
    [InlineData("White Sandwich Bread", "20 oz", "Nature's Own Whole Grain White Sandwich Bread", "grain")]
    [InlineData("Boneless Skinless Chicken Breast", "1 lb", "Tyson Breaded Boneless Skinless Chicken Breasts", "breaded")]
    public async Task EvaluateAsync_WhenProductIsAnotherVariantOfTheFood_ReturnsNoMatch(
        string itemName, string itemSize, string productName, string variant)
    {
        // Arrange
        var item = CreateItem(id: 1, name: itemName, size: itemSize);
        var providerProduct = CreateProviderProduct(name: productName, size: itemSize);
        SetupExistingProducts(item.Id, new List<RetailerProduct>());

        // Act
        var result = await _service.EvaluateAsync(item, providerProduct);

        // Assert
        Assert.False(result.IsMatch);
        Assert.Contains(variant, result.Reason);
    }

    [Theory]
    [InlineData("Fresh Bunch of Bananas – 5-7 Bananas", "per lb", true)]
    [InlineData("Banana (Bananas are sold individually) (Avg. 0.4lb)", "per lb", true)]
    [InlineData("Banana Squash", "per lb", false)]
    [InlineData("Fresh Yellow Banana Peppers", "per lb", false)]
    [InlineData("Harris Teeter Farmers Market™ Banana Chips", "7 oz", false)]
    [InlineData("Simple Truth® Freeze-Dried Bananas", "1.5 oz", false)]
    [InlineData("Fresh Bunch of Organic Bananas – 5-7 Bananas", "per lb", false)]
    public async Task EvaluateAsync_WhenOneWordItemIsSoldPerPound_MatchesOnlyProductsNamedForIt(
        string productName, string productSize, bool expectedMatch)
    {
        // Arrange
        var item = CreateItem(id: 1, name: "Bananas", size: "per lb");
        var providerProduct = CreateProviderProduct(name: productName, size: productSize);
        SetupExistingProducts(item.Id, new List<RetailerProduct>());

        // Act
        var result = await _service.EvaluateAsync(item, providerProduct);

        // Assert
        Assert.Equal(expectedMatch, result.IsMatch);
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