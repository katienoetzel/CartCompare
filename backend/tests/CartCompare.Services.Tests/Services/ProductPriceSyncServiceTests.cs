using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class ProductPriceSyncServiceTests
{
    private readonly Mock<IItemRepository>
        _itemRepositoryMock;

    private readonly Mock<IStoreLocationRepository>
        _storeLocationRepositoryMock;

    private readonly Mock<IRetailerRepository>
        _retailerRepositoryMock;

    private readonly Mock<IRetailerProductRepository>
        _retailerProductRepositoryMock;

    private readonly Mock<IPriceProviderResolver>
        _providerResolverMock;

    private readonly Mock<IPriceProvider>
        _providerMock;

    private readonly Mock<IPriceService>
        _priceServiceMock;

    private readonly Mock<IProductMatchingService>
        _productMatchingServiceMock;

    private readonly ProductPriceSyncService
        _service;

    public ProductPriceSyncServiceTests()
    {
        _itemRepositoryMock =
            new Mock<IItemRepository>();

        _storeLocationRepositoryMock =
            new Mock<IStoreLocationRepository>();

        _retailerRepositoryMock =
            new Mock<IRetailerRepository>();

        _retailerProductRepositoryMock =
            new Mock<IRetailerProductRepository>();

        _providerResolverMock =
            new Mock<IPriceProviderResolver>();

        _providerMock =
            new Mock<IPriceProvider>();

        _priceServiceMock =
            new Mock<IPriceService>();

        _productMatchingServiceMock =
            new Mock<IProductMatchingService>();

        _service =
            new ProductPriceSyncService(
                _itemRepositoryMock.Object,
                _storeLocationRepositoryMock.Object,
                _retailerRepositoryMock.Object,
                _retailerProductRepositoryMock.Object,
                _providerResolverMock.Object,
                _priceServiceMock.Object,
                _productMatchingServiceMock.Object
            );
    }

    [Fact]
    public async Task SyncAsync_WhenItemDoesNotExist_ReturnsItemNotFound()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(1)
            )
            .ReturnsAsync((Item?)null);

        // Act
        var result =
            await _service.SyncAsync(
                1,
                10,
                "PRODUCT-123"
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType.ItemNotFound,
            result.Result
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenStoreDoesNotExist_ReturnsStoreLocationNotFound()
    {
        // Arrange
        var item =
            CreateItem();

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(item.Id)
            )
            .ReturnsAsync(item);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(10)
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                10,
                "PRODUCT-123"
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType
                .StoreLocationNotFound,
            result.Result
        );

        _retailerRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenRetailerDoesNotExist_ReturnsRetailerNotFound()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(item.Id)
            )
            .ReturnsAsync(item);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(store.Id)
            )
            .ReturnsAsync(store);

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.RetailerId
                )
            )
            .ReturnsAsync(
                (Retailer?)null
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                "PRODUCT-123"
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType
                .RetailerNotFound,
            result.Result
        );

        _providerResolverMock.Verify(
            resolver =>
                resolver.Resolve(
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenProviderDoesNotExist_ReturnsProviderNotFound()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        SetupValidBaseData(
            item,
            store,
            retailer
        );

        _providerResolverMock
            .Setup(resolver =>
                resolver.Resolve(
                    retailer.Name
                )
            )
            .Returns(
                (IPriceProvider?)null
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                "PRODUCT-123"
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType
                .ProviderNotFound,
            result.Result
        );
    }

    [Fact]
    public async Task SyncAsync_WhenProviderProductDoesNotExist_ReturnsProviderProductNotFound()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        SetupValidBaseData(
            item,
            store,
            retailer
        );

        SetupProvider(
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.GetProductAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    "PRODUCT-123"
                )
            )
            .ReturnsAsync(
                (ProviderProduct?)null
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                "PRODUCT-123"
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType
                .ProviderProductNotFound,
            result.Result
        );

        Assert.Equal(
            "KrogerPublicApi",
            result.ProviderName
        );

        _productMatchingServiceMock.Verify(
            service =>
                service.EvaluateAsync(
                    It.IsAny<Item>(),
                    It.IsAny<ProviderProduct>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenProductAlreadyBelongsToDifferentItem_ReturnsConflict()
    {
        // Arrange
        var item =
            CreateItem(id: 1);

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var providerProduct =
            CreateProviderProduct();

        var existingProduct =
            CreateRetailerProduct(
                itemId: 999
            );

        SetupSuccessfulProductLookup(
            item,
            store,
            retailer,
            providerProduct
        );

        SetupMatch(
            isMatch: true,
            ProductMatchMethod.Deterministic,
            0.90m
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        providerProduct.ExternalProductId
                    )
            )
            .ReturnsAsync(existingProduct);

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                providerProduct.ExternalProductId
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType
                .ProductMatchedToDifferentItem,
            result.Result
        );

        Assert.Same(
            existingProduct,
            result.RetailerProduct
        );

        _providerMock.Verify(
            provider =>
                provider.GetPriceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()
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

        _retailerProductRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    It.IsAny<RetailerProduct>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenNewProductHasDeterministicMatch_CreatesProductAndPrice()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var providerProduct =
            CreateProviderProduct();

        var providerPrice =
            CreateProviderPrice();

        SetupSuccessfulProductLookup(
            item,
            store,
            retailer,
            providerProduct
        );

        SetupMatch(
            isMatch: true,
            ProductMatchMethod.Deterministic,
            0.90m
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        providerProduct.ExternalProductId
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
                    // EF Core would assign the generated
                    // database ID after SaveChangesAsync.
                    // Our mock must simulate that behavior.
                    product.Id = 50;
                    addedProduct = product;
                }
            )
            .Returns(Task.CompletedTask);

        _providerMock
            .Setup(provider =>
                provider.GetPriceAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    providerProduct.ExternalProductId
                )
            )
            .ReturnsAsync(providerPrice);

        var storedPrice =
            CreateStoredPrice(
                retailerProductId: 50,
                storeLocationId: store.Id
            );

        _priceServiceMock
            .Setup(service =>
                service.UpsertAsync(
                    50,
                    store.Id,
                    providerPrice.RegularPrice,
                    providerPrice.SalePrice,
                    providerPrice.MemberPrice,
                    providerPrice.AvailabilityStatus,
                    "KrogerPublicApi",
                    providerPrice.SourceUpdatedAt
                )
            )
            .ReturnsAsync(
                new PriceUpsertResult
                {
                    Result =
                        PriceUpsertResultType.Created,

                    StoredPrice =
                        storedPrice
                }
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                providerProduct.ExternalProductId
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType.Succeeded,
            result.Result
        );

        Assert.True(result.ProductCreated);
        Assert.False(result.ProductUpdated);
        Assert.True(result.PriceSynced);

        Assert.NotNull(addedProduct);

        Assert.Equal(
            item.Id,
            addedProduct.ItemId
        );

        Assert.Equal(
            retailer.Id,
            addedProduct.RetailerId
        );

        Assert.Equal(
            providerProduct.ExternalProductId,
            addedProduct.ExternalProductId
        );

        Assert.Equal(
            providerProduct.Name,
            addedProduct.Name
        );

        Assert.Equal(
            ProductMatchMethod.Deterministic,
            addedProduct.MatchMethod
        );

        Assert.Equal(
            0.90m,
            addedProduct.MatchConfidence
        );

        Assert.Same(
            storedPrice,
            result.Price
        );
    }

    [Fact]
    public async Task SyncAsync_WhenNewProductIsNotSafelyMatched_StoresItAsManual()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var providerProduct =
            CreateProviderProduct();

        SetupSuccessfulProductLookup(
            item,
            store,
            retailer,
            providerProduct
        );

        SetupMatch(
            isMatch: false,
            matchMethod: null,
            confidence: null
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        providerProduct.ExternalProductId
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

        _providerMock
            .Setup(provider =>
                provider.GetPriceAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    providerProduct.ExternalProductId
                )
            )
            .ReturnsAsync(
                (ProviderPriceSnapshot?)null
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                providerProduct.ExternalProductId
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType
                .PriceNotAvailable,
            result.Result
        );

        Assert.True(result.ProductCreated);
        Assert.False(result.PriceSynced);

        Assert.NotNull(addedProduct);

        Assert.Equal(
            ProductMatchMethod.Manual,
            addedProduct.MatchMethod
        );

        Assert.Null(
            addedProduct.MatchConfidence
        );
    }

    [Fact]
    public async Task SyncAsync_WhenExistingProductIsRefreshed_PreservesOriginalMatchProvenance()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var providerProduct =
            CreateProviderProduct(
                name: "Updated Kroger Whole Milk"
            );

        var existingProduct =
            CreateRetailerProduct(
                itemId: item.Id
            );

        existingProduct.MatchMethod =
            ProductMatchMethod.Manual;

        existingProduct.MatchConfidence =
            null;

        existingProduct.Name =
            "Old Product Name";

        SetupSuccessfulProductLookup(
            item,
            store,
            retailer,
            providerProduct
        );

        // Even though the current matcher now thinks this
        // could be deterministic, refreshing an existing
        // record must not rewrite its original provenance.
        SetupMatch(
            isMatch: true,
            ProductMatchMethod.Deterministic,
            0.98m
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        providerProduct.ExternalProductId
                    )
            )
            .ReturnsAsync(existingProduct);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.UpdateAsync(
                    existingProduct
                )
            )
            .Returns(Task.CompletedTask);

        var providerPrice =
            CreateProviderPrice();

        _providerMock
            .Setup(provider =>
                provider.GetPriceAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    providerProduct.ExternalProductId
                )
            )
            .ReturnsAsync(providerPrice);

        var storedPrice =
            CreateStoredPrice(
                existingProduct.Id,
                store.Id
            );

        _priceServiceMock
            .Setup(service =>
                service.UpsertAsync(
                    existingProduct.Id,
                    store.Id,
                    providerPrice.RegularPrice,
                    providerPrice.SalePrice,
                    providerPrice.MemberPrice,
                    providerPrice.AvailabilityStatus,
                    "KrogerPublicApi",
                    providerPrice.SourceUpdatedAt
                )
            )
            .ReturnsAsync(
                new PriceUpsertResult
                {
                    Result =
                        PriceUpsertResultType.Updated,

                    StoredPrice =
                        storedPrice
                }
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                providerProduct.ExternalProductId
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType.Succeeded,
            result.Result
        );

        Assert.False(result.ProductCreated);
        Assert.True(result.ProductUpdated);
        Assert.True(result.PriceSynced);

        Assert.Equal(
            providerProduct.Name,
            existingProduct.Name
        );

        Assert.Equal(
            providerProduct.Brand,
            existingProduct.Brand
        );

        Assert.Equal(
            providerProduct.Size,
            existingProduct.Size
        );

        Assert.Equal(
            providerProduct.Upc,
            existingProduct.Upc
        );

        Assert.True(
            existingProduct.IsActive
        );

        Assert.Equal(
            ProductMatchMethod.Manual,
            existingProduct.MatchMethod
        );

        Assert.Null(
            existingProduct.MatchConfidence
        );

        _retailerProductRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    existingProduct
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task SyncAsync_WhenProviderReturnsNoPrice_ReturnsPriceNotAvailable()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var providerProduct =
            CreateProviderProduct();

        var existingProduct =
            CreateRetailerProduct(
                itemId: item.Id
            );

        SetupSuccessfulProductLookup(
            item,
            store,
            retailer,
            providerProduct
        );

        SetupMatch(
            true,
            ProductMatchMethod.Deterministic,
            0.90m
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        providerProduct.ExternalProductId
                    )
            )
            .ReturnsAsync(existingProduct);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.UpdateAsync(
                    existingProduct
                )
            )
            .Returns(Task.CompletedTask);

        _providerMock
            .Setup(provider =>
                provider.GetPriceAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    providerProduct.ExternalProductId
                )
            )
            .ReturnsAsync(
                (ProviderPriceSnapshot?)null
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                providerProduct.ExternalProductId
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType
                .PriceNotAvailable,
            result.Result
        );

        Assert.True(result.ProductUpdated);
        Assert.False(result.PriceSynced);

        _priceServiceMock.Verify(
            service =>
                service.UpsertAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<AvailabilityStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenPriceUpsertFails_ReturnsPriceSyncFailed()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var providerProduct =
            CreateProviderProduct();

        var existingProduct =
            CreateRetailerProduct(
                itemId: item.Id
            );

        var providerPrice =
            CreateProviderPrice();

        SetupSuccessfulProductLookup(
            item,
            store,
            retailer,
            providerProduct
        );

        SetupMatch(
            true,
            ProductMatchMethod.Deterministic,
            0.90m
        );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        providerProduct.ExternalProductId
                    )
            )
            .ReturnsAsync(existingProduct);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.UpdateAsync(
                    existingProduct
                )
            )
            .Returns(Task.CompletedTask);

        _providerMock
            .Setup(provider =>
                provider.GetPriceAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    providerProduct.ExternalProductId
                )
            )
            .ReturnsAsync(providerPrice);

        _priceServiceMock
            .Setup(service =>
                service.UpsertAsync(
                    existingProduct.Id,
                    store.Id,
                    providerPrice.RegularPrice,
                    providerPrice.SalePrice,
                    providerPrice.MemberPrice,
                    providerPrice.AvailabilityStatus,
                    "KrogerPublicApi",
                    providerPrice.SourceUpdatedAt
                )
            )
            .ReturnsAsync(
                new PriceUpsertResult
                {
                    Result =
                        PriceUpsertResultType.InvalidPrice
                }
            );

        // Act
        var result =
            await _service.SyncAsync(
                item.Id,
                store.Id,
                providerProduct.ExternalProductId
            );

        // Assert
        Assert.Equal(
            ProductPriceSyncResultType
                .PriceSyncFailed,
            result.Result
        );

        Assert.True(result.ProductUpdated);
        Assert.False(result.PriceSynced);

        Assert.Equal(
            "KrogerPublicApi",
            result.ProviderName
        );
    }

    private void SetupValidBaseData(
        Item item,
        StoreLocation store,
        Retailer retailer)
    {
        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.Id
                )
            )
            .ReturnsAsync(store);

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.RetailerId
                )
            )
            .ReturnsAsync(retailer);
    }

    private void SetupProvider(
        Retailer retailer)
    {
        _providerMock
            .SetupGet(provider =>
                provider.ProviderName
            )
            .Returns(
                "KrogerPublicApi"
            );

        _providerResolverMock
            .Setup(resolver =>
                resolver.Resolve(
                    retailer.Name
                )
            )
            .Returns(
                _providerMock.Object
            );
    }

    private void SetupSuccessfulProductLookup(
        Item item,
        StoreLocation store,
        Retailer retailer,
        ProviderProduct providerProduct)
    {
        SetupValidBaseData(
            item,
            store,
            retailer
        );

        SetupProvider(
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.GetProductAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    providerProduct.ExternalProductId
                )
            )
            .ReturnsAsync(
                providerProduct
            );
    }

    private void SetupMatch(
        bool isMatch,
        ProductMatchMethod? matchMethod,
        decimal? confidence)
    {
        _productMatchingServiceMock
            .Setup(service =>
                service.EvaluateAsync(
                    It.IsAny<Item>(),
                    It.IsAny<ProviderProduct>()
                )
            )
            .ReturnsAsync(
                new ProductMatchResult
                {
                    IsMatch =
                        isMatch,

                    MatchMethod =
                        matchMethod,

                    MatchConfidence =
                        confidence,

                    Reason =
                        isMatch
                            ? "Test match."
                            : "Test no match."
                }
            );
    }

    private static Item CreateItem(
        int id = 1)
    {
        return new Item
        {
            Id = id,
            Name = "Whole Milk",
            Size = "1 gallon",
            IsActive = true
        };
    }

    private static Retailer CreateRetailer()
    {
        return new Retailer
        {
            Id = 1,
            Name = "Kroger",
            SupportsMembership = true,
            IsActive = true
        };
    }

    private static StoreLocation CreateStore()
    {
        return new StoreLocation
        {
            Id = 10,
            RetailerId = 1,

            ExternalLocationId =
                "KROGER-STORE-123",

            Name =
                "Test Kroger",

            AddressLine1 =
                "123 Test Street",

            City =
                "Cincinnati",

            State =
                "OH",

            PostalCode =
                "45202",

            IsActive =
                true
        };
    }

    private static ProviderProduct
        CreateProviderProduct(
            string name = "Kroger Whole Milk")
    {
        return new ProviderProduct
        {
            ExternalProductId =
                "PRODUCT-123",

            Name =
                name,

            Brand =
                "Kroger",

            Size =
                "1 gal",

            Upc =
                "012345678905"
        };
    }

    private static RetailerProduct
        CreateRetailerProduct(
            int itemId)
    {
        return new RetailerProduct
        {
            Id = 25,

            ItemId =
                itemId,

            RetailerId =
                1,

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

            IsActive =
                true
        };
    }

    private static ProviderPriceSnapshot
        CreateProviderPrice()
    {
        return new ProviderPriceSnapshot
        {
            RegularPrice =
                4.29m,

            SalePrice =
                3.99m,

            MemberPrice =
                null,

            AvailabilityStatus =
                AvailabilityStatus.Available,

            SourceUpdatedAt =
                null
        };
    }

    private static Price CreateStoredPrice(
        int retailerProductId,
        int storeLocationId)
    {
        return new Price
        {
            Id = 100,

            RetailerProductId =
                retailerProductId,

            StoreLocationId =
                storeLocationId,

            RegularPrice =
                4.29m,

            SalePrice =
                3.99m,

            MemberPrice =
                null,

            AvailabilityStatus =
                AvailabilityStatus.Available,

            SourceProvider =
                "KrogerPublicApi",

            LastCheckedAt =
                DateTime.UtcNow,

            UpdatedAt =
                DateTime.UtcNow
        };
    }
}