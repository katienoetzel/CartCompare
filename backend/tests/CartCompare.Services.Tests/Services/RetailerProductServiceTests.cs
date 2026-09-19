using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class RetailerProductServiceTests
{
    private readonly Mock<IRetailerProductRepository>
        _retailerProductRepositoryMock;

    private readonly Mock<IRetailerRepository>
        _retailerRepositoryMock;

    private readonly Mock<IItemRepository>
        _itemRepositoryMock;

    private readonly RetailerProductService
        _service;

    public RetailerProductServiceTests()
    {
        _retailerProductRepositoryMock =
            new Mock<IRetailerProductRepository>();

        _retailerRepositoryMock =
            new Mock<IRetailerRepository>();

        _itemRepositoryMock =
            new Mock<IItemRepository>();

        _service =
            new RetailerProductService(
                _retailerProductRepositoryMock.Object,
                _retailerRepositoryMock.Object,
                _itemRepositoryMock.Object
            );
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductExists_ReturnsProduct()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    product.Id
                )
            )
            .ReturnsAsync(product);

        // Act
        var result =
            await _service.GetByIdAsync(
                product.Id
            );

        // Assert
        Assert.Same(
            product,
            result
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductDoesNotExist_ReturnsNull()
    {
        // Arrange
        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    999
                )
            )
            .ReturnsAsync(
                (RetailerProduct?)null
            );

        // Act
        var result =
            await _service.GetByIdAsync(
                999
            );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByRetailerIdAsync_ReturnsRepositoryResults()
    {
        // Arrange
        var products =
            new List<RetailerProduct>
            {
                CreateRetailerProduct(
                    id: 1
                ),
                CreateRetailerProduct(
                    id: 2
                )
            };

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByRetailerIdAsync(
                    1
                )
            )
            .ReturnsAsync(products);

        // Act
        var result =
            await _service.GetByRetailerIdAsync(
                1
            );

        // Assert
        Assert.Same(
            products,
            result
        );

        Assert.Equal(
            2,
            result.Count
        );
    }

    [Fact]
    public async Task GetByItemIdAsync_ReturnsRepositoryResults()
    {
        // Arrange
        var products =
            new List<RetailerProduct>
            {
                CreateRetailerProduct(
                    id: 1
                ),
                CreateRetailerProduct(
                    id: 2
                )
            };

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    1
                )
            )
            .ReturnsAsync(products);

        // Act
        var result =
            await _service.GetByItemIdAsync(
                1
            );

        // Assert
        Assert.Same(
            products,
            result
        );

        Assert.Equal(
            2,
            result.Count
        );
    }

    [Fact]
    public async Task CreateAsync_WhenRetailerDoesNotExist_ReturnsRetailerNotFound()
    {
        // Arrange
        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    999
                )
            )
            .ReturnsAsync(
                (Retailer?)null
            );

        // Act
        var result =
            await _service.CreateAsync(
                itemId: 1,
                retailerId: 999,
                externalProductId:
                    "PRODUCT-123",
                name: "Whole Milk",
                brand: "Kroger",
                size: "1 gal",
                upc: "012345678905"
            );

        // Assert
        Assert.Equal(
            RetailerProductCreateResult
                .RetailerNotFound,
            result.Result
        );

        Assert.Null(
            result.RetailerProduct
        );

        _itemRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );

        _retailerProductRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<RetailerProduct>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateAsync_WhenItemDoesNotExist_ReturnsItemNotFound()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    retailer.Id
                )
            )
            .ReturnsAsync(retailer);

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    999
                )
            )
            .ReturnsAsync(
                (Item?)null
            );

        // Act
        var result =
            await _service.CreateAsync(
                itemId: 999,
                retailerId: retailer.Id,
                externalProductId:
                    "PRODUCT-123",
                name: "Whole Milk",
                brand: null,
                size: null,
                upc: null
            );

        // Assert
        Assert.Equal(
            RetailerProductCreateResult
                .ItemNotFound,
            result.Result
        );

        Assert.Null(
            result.RetailerProduct
        );

        _retailerProductRepositoryMock.Verify(
            repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        It.IsAny<int>(),
                        It.IsAny<string>()
                    ),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateAsync_WhenProductAlreadyExists_ReturnsAlreadyExists()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        var item =
            CreateItem();

        var existingProduct =
            CreateRetailerProduct();

        SetupRetailerAndItem(
            retailer,
            item
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "PRODUCT-123"
                    )
            )
            .ReturnsAsync(
                existingProduct
            );

        // Act
        var result =
            await _service.CreateAsync(
                item.Id,
                retailer.Id,
                "  PRODUCT-123  ",
                "Whole Milk",
                "Kroger",
                "1 gal",
                "012345678905"
            );

        // Assert
        Assert.Equal(
            RetailerProductCreateResult
                .AlreadyExists,
            result.Result
        );

        Assert.Same(
            existingProduct,
            result.RetailerProduct
        );

        _retailerProductRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<RetailerProduct>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesManualActiveRetailerProduct()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        var item =
            CreateItem();

        SetupRetailerAndItem(
            retailer,
            item
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "PRODUCT-123"
                    )
            )
            .ReturnsAsync(
                (RetailerProduct?)null
            );

        RetailerProduct? addedProduct =
            null;

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<RetailerProduct>()
                )
            )
            .Callback<RetailerProduct>(
                product =>
                {
                    product.Id = 50;
                    addedProduct = product;
                }
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.CreateAsync(
                item.Id,
                retailer.Id,
                "  PRODUCT-123  ",
                "  Kroger Whole Milk  ",
                "  Kroger  ",
                "  1 gal  ",
                "  012345678905  "
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.Equal(
            RetailerProductCreateResult.Created,
            result.Result
        );

        Assert.NotNull(
            addedProduct
        );

        Assert.Equal(
            item.Id,
            addedProduct.ItemId
        );

        Assert.Equal(
            retailer.Id,
            addedProduct.RetailerId
        );

        Assert.Equal(
            "PRODUCT-123",
            addedProduct.ExternalProductId
        );

        Assert.Equal(
            "Kroger Whole Milk",
            addedProduct.Name
        );

        Assert.Equal(
            "Kroger",
            addedProduct.Brand
        );

        Assert.Equal(
            "1 gal",
            addedProduct.Size
        );

        Assert.Equal(
            "012345678905",
            addedProduct.Upc
        );

        Assert.Equal(
            ProductMatchMethod.Manual,
            addedProduct.MatchMethod
        );

        Assert.Null(
            addedProduct.MatchConfidence
        );

        Assert.True(
            addedProduct.IsActive
        );

        Assert.InRange(
            addedProduct.LastSeenAt,
            before,
            after
        );

        Assert.Same(
            addedProduct,
            result.RetailerProduct
        );
    }

    [Fact]
    public async Task CreateAsync_WhenOptionalMetadataIsNull_PreservesNullValues()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        var item =
            CreateItem();

        SetupRetailerAndItem(
            retailer,
            item
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "PRODUCT-123"
                    )
            )
            .ReturnsAsync(
                (RetailerProduct?)null
            );

        RetailerProduct? addedProduct =
            null;

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<RetailerProduct>()
                )
            )
            .Callback<RetailerProduct>(
                product =>
                    addedProduct = product
            )
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateAsync(
            item.Id,
            retailer.Id,
            "PRODUCT-123",
            "Whole Milk",
            null,
            null,
            null
        );

        // Assert
        Assert.NotNull(
            addedProduct
        );

        Assert.Null(
            addedProduct.Brand
        );

        Assert.Null(
            addedProduct.Size
        );

        Assert.Null(
            addedProduct.Upc
        );
    }

    private void SetupRetailerAndItem(
        Retailer retailer,
        Item item)
    {
        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    retailer.Id
                )
            )
            .ReturnsAsync(retailer);

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);
    }

    private static Retailer CreateRetailer()
    {
        return new Retailer
        {
            Id = 1,
            Name = "Kroger",
            SupportsMembership = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Item CreateItem()
    {
        return new Item
        {
            Id = 1,
            Name = "Whole Milk",
            Size = "1 gallon",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static RetailerProduct
        CreateRetailerProduct(
            int id = 10)
    {
        return new RetailerProduct
        {
            Id = id,
            ItemId = 1,
            RetailerId = 1,

            ExternalProductId =
                "PRODUCT-123",

            Name =
                "Kroger Whole Milk",

            Brand =
                "Kroger",

            Size =
                "1 gal",

            Upc =
                "012345678905",

            MatchMethod =
                ProductMatchMethod.Manual,

            MatchConfidence =
                null,

            IsActive =
                true,

            LastSeenAt =
                DateTime.UtcNow
        };
    }
}