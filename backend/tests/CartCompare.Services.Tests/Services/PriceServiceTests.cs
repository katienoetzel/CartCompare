using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class PriceServiceTests
{
    private readonly Mock<IPriceRepository>
        _priceRepositoryMock;

    private readonly Mock<IRetailerProductRepository>
        _retailerProductRepositoryMock;

    private readonly Mock<IStoreLocationRepository>
        _storeLocationRepositoryMock;

    private readonly Mock<IUserRetailerMembershipRepository>
        _membershipRepositoryMock;

    private readonly PriceService
        _service;

    public PriceServiceTests()
    {
        _priceRepositoryMock =
            new Mock<IPriceRepository>();

        _retailerProductRepositoryMock =
            new Mock<IRetailerProductRepository>();

        _storeLocationRepositoryMock =
            new Mock<IStoreLocationRepository>();

        _membershipRepositoryMock =
            new Mock<IUserRetailerMembershipRepository>();

        _service =
            new PriceService(
                _priceRepositoryMock.Object,
                _retailerProductRepositoryMock.Object,
                _storeLocationRepositoryMock.Object,
                _membershipRepositoryMock.Object
            );
    }

    // ----------------------------------------------------
    // UPSERT VALIDATION
    // ----------------------------------------------------

    [Fact]
    public async Task UpsertAsync_WhenRegularPriceIsNegative_ReturnsInvalidPrice()
    {
        // Act
        var result =
            await _service.UpsertAsync(
                retailerProductId: 1,
                storeLocationId: 10,
                regularPrice: -1.00m,
                salePrice: null,
                memberPrice: null,
                availabilityStatus:
                    AvailabilityStatus.Available,
                sourceProvider: "TestProvider",
                sourceUpdatedAt: null
            );

        // Assert
        Assert.Equal(
            PriceUpsertResultType.InvalidPrice,
            result.Result
        );

        Assert.Null(result.StoredPrice);

        VerifyNoProductLookup();
    }

    [Fact]
    public async Task UpsertAsync_WhenSalePriceIsNegative_ReturnsInvalidPrice()
    {
        // Act
        var result =
            await _service.UpsertAsync(
                retailerProductId: 1,
                storeLocationId: 10,
                regularPrice: 4.00m,
                salePrice: -1.00m,
                memberPrice: null,
                availabilityStatus:
                    AvailabilityStatus.Available,
                sourceProvider: "TestProvider",
                sourceUpdatedAt: null
            );

        // Assert
        Assert.Equal(
            PriceUpsertResultType.InvalidPrice,
            result.Result
        );

        VerifyNoProductLookup();
    }

    [Fact]
    public async Task UpsertAsync_WhenMemberPriceIsNegative_ReturnsInvalidPrice()
    {
        // Act
        var result =
            await _service.UpsertAsync(
                retailerProductId: 1,
                storeLocationId: 10,
                regularPrice: 4.00m,
                salePrice: 3.50m,
                memberPrice: -1.00m,
                availabilityStatus:
                    AvailabilityStatus.Available,
                sourceProvider: "TestProvider",
                sourceUpdatedAt: null
            );

        // Assert
        Assert.Equal(
            PriceUpsertResultType.InvalidPrice,
            result.Result
        );

        VerifyNoProductLookup();
    }

    [Fact]
    public async Task UpsertAsync_WhenRetailerProductDoesNotExist_ReturnsRetailerProductNotFound()
    {
        // Arrange
        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(1)
            )
            .ReturnsAsync(
                (RetailerProduct?)null
            );

        // Act
        var result =
            await _service.UpsertAsync(
                1,
                10,
                4.00m,
                null,
                null,
                AvailabilityStatus.Available,
                "TestProvider",
                null
            );

        // Assert
        Assert.Equal(
            PriceUpsertResultType
                .RetailerProductNotFound,
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
    public async Task UpsertAsync_WhenStoreDoesNotExist_ReturnsStoreLocationNotFound()
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

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(10)
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

        // Act
        var result =
            await _service.UpsertAsync(
                product.Id,
                10,
                4.00m,
                null,
                null,
                AvailabilityStatus.Available,
                "TestProvider",
                null
            );

        // Assert
        Assert.Equal(
            PriceUpsertResultType
                .StoreLocationNotFound,
            result.Result
        );

        _priceRepositoryMock.Verify(
            repository =>
                repository.GetByProductAndStoreAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task UpsertAsync_WhenProductAndStoreRetailersDiffer_ReturnsRetailerMismatch()
    {
        // Arrange
        var product =
            CreateRetailerProduct(
                retailerId: 1
            );

        var store =
            CreateStore(
                retailerId: 2
            );

        SetupProductAndStore(
            product,
            store
        );

        // Act
        var result =
            await _service.UpsertAsync(
                product.Id,
                store.Id,
                4.00m,
                null,
                null,
                AvailabilityStatus.Available,
                "TestProvider",
                null
            );

        // Assert
        Assert.Equal(
            PriceUpsertResultType
                .RetailerMismatch,
            result.Result
        );

        _priceRepositoryMock.Verify(
            repository =>
                repository.GetByProductAndStoreAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    // ----------------------------------------------------
    // CREATE / UPDATE
    // ----------------------------------------------------

    [Fact]
    public async Task UpsertAsync_WhenPriceDoesNotExist_CreatesPrice()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var store =
            CreateStore();

        SetupProductAndStore(
            product,
            store
        );

        _priceRepositoryMock
            .Setup(repository =>
                repository.GetByProductAndStoreAsync(
                    product.Id,
                    store.Id
                )
            )
            .ReturnsAsync(
                (Price?)null
            );

        Price? addedPrice = null;

        _priceRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<Price>()
                )
            )
            .Callback<Price>(
                price =>
                {
                    price.Id = 100;
                    addedPrice = price;
                }
            )
            .Returns(Task.CompletedTask);

        var sourceUpdatedAt =
            new DateTime(
                2026,
                9,
                15,
                12,
                0,
                0,
                DateTimeKind.Utc
            );

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.UpsertAsync(
                product.Id,
                store.Id,
                regularPrice: 4.29m,
                salePrice: 3.99m,
                memberPrice: 3.49m,
                availabilityStatus:
                    AvailabilityStatus.Available,
                sourceProvider:
                    "TestProvider",
                sourceUpdatedAt:
                    sourceUpdatedAt
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.Equal(
            PriceUpsertResultType.Created,
            result.Result
        );

        Assert.NotNull(addedPrice);

        Assert.Equal(
            product.Id,
            addedPrice.RetailerProductId
        );

        Assert.Equal(
            store.Id,
            addedPrice.StoreLocationId
        );

        Assert.Equal(
            4.29m,
            addedPrice.RegularPrice
        );

        Assert.Equal(
            3.99m,
            addedPrice.SalePrice
        );

        Assert.Equal(
            3.49m,
            addedPrice.MemberPrice
        );

        Assert.Equal(
            AvailabilityStatus.Available,
            addedPrice.AvailabilityStatus
        );

        Assert.Equal(
            "TestProvider",
            addedPrice.SourceProvider
        );

        Assert.Equal(
            sourceUpdatedAt,
            addedPrice.SourceUpdatedAt
        );

        Assert.InRange(
            addedPrice.LastCheckedAt,
            before,
            after
        );

        Assert.InRange(
            addedPrice.UpdatedAt,
            before,
            after
        );

        Assert.Same(
            addedPrice,
            result.StoredPrice
        );

        _priceRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<Price>()
                ),
            Times.Once
        );

        _priceRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    It.IsAny<Price>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task UpsertAsync_WhenPriceExists_UpdatesExistingPrice()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var store =
            CreateStore();

        var existingPrice =
            CreatePrice(
                regularPrice: 5.00m,
                salePrice: null,
                memberPrice: null
            );

        SetupProductAndStore(
            product,
            store
        );

        _priceRepositoryMock
            .Setup(repository =>
                repository.GetByProductAndStoreAsync(
                    product.Id,
                    store.Id
                )
            )
            .ReturnsAsync(existingPrice);

        _priceRepositoryMock
            .Setup(repository =>
                repository.UpdateAsync(
                    existingPrice
                )
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.UpsertAsync(
                product.Id,
                store.Id,
                regularPrice: 4.50m,
                salePrice: 3.75m,
                memberPrice: 3.25m,
                availabilityStatus:
                    AvailabilityStatus.Unknown,
                sourceProvider:
                    "UpdatedProvider",
                sourceUpdatedAt:
                    null
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.Equal(
            PriceUpsertResultType.Updated,
            result.Result
        );

        Assert.Equal(
            4.50m,
            existingPrice.RegularPrice
        );

        Assert.Equal(
            3.75m,
            existingPrice.SalePrice
        );

        Assert.Equal(
            3.25m,
            existingPrice.MemberPrice
        );

        Assert.Equal(
            AvailabilityStatus.Unknown,
            existingPrice.AvailabilityStatus
        );

        Assert.Equal(
            "UpdatedProvider",
            existingPrice.SourceProvider
        );

        Assert.Null(
            existingPrice.SourceUpdatedAt
        );

        Assert.InRange(
            existingPrice.LastCheckedAt,
            before,
            after
        );

        Assert.InRange(
            existingPrice.UpdatedAt,
            before,
            after
        );

        Assert.Same(
            existingPrice,
            result.StoredPrice
        );

        _priceRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    existingPrice
                ),
            Times.Once
        );

        _priceRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<Price>()
                ),
            Times.Never
        );
    }

    // ----------------------------------------------------
    // EFFECTIVE PRICE
    // ----------------------------------------------------

    [Fact]
    public async Task GetEffectivePriceAsync_WhenCachedPriceDoesNotExist_ReturnsNull()
    {
        // Arrange
        _priceRepositoryMock
            .Setup(repository =>
                repository.GetByProductAndStoreAsync(
                    1,
                    10
                )
            )
            .ReturnsAsync(
                (Price?)null
            );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                userId: 20,
                retailerProductId: 1,
                storeLocationId: 10
            );

        // Assert
        Assert.Null(result);

        _retailerProductRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GetEffectivePriceAsync_WhenRetailerProductDoesNotExist_ReturnsNull()
    {
        // Arrange
        var price =
            CreatePrice();

        _priceRepositoryMock
            .Setup(repository =>
                repository.GetByProductAndStoreAsync(
                    price.RetailerProductId,
                    price.StoreLocationId
                )
            )
            .ReturnsAsync(price);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    price.RetailerProductId
                )
            )
            .ReturnsAsync(
                (RetailerProduct?)null
            );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                price.RetailerProductId,
                price.StoreLocationId
            );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetEffectivePriceAsync_WhenUserHasNoMembership_IgnoresMemberPrice()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var price =
            CreatePrice(
                regularPrice: 4.29m,
                salePrice: 3.99m,
                memberPrice: 2.49m
            );

        SetupEffectivePriceData(
            userId: 20,
            product,
            price,
            membership: null
        );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                product.Id,
                price.StoreLocationId
            );

        // Assert
        Assert.NotNull(result);

        Assert.False(
            result.HasRetailerMembership
        );

        Assert.Equal(
            3.99m,
            result.Amount
        );

        Assert.Equal(
            EffectivePriceType.Sale,
            result.PriceType
        );
    }

    [Fact]
    public async Task GetEffectivePriceAsync_WhenUserHasMembership_MemberPriceCanWin()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var price =
            CreatePrice(
                regularPrice: 4.29m,
                salePrice: 3.99m,
                memberPrice: 2.99m
            );

        var membership =
            CreateMembership(
                userId: 20,
                retailerId:
                    product.RetailerId
            );

        SetupEffectivePriceData(
            20,
            product,
            price,
            membership
        );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                product.Id,
                price.StoreLocationId
            );

        // Assert
        Assert.NotNull(result);

        Assert.True(
            result.HasRetailerMembership
        );

        Assert.Equal(
            2.99m,
            result.Amount
        );

        Assert.Equal(
            EffectivePriceType.Member,
            result.PriceType
        );
    }

    [Fact]
    public async Task GetEffectivePriceAsync_WhenMemberPriceIsNotCheapest_SalePriceStillWins()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var price =
            CreatePrice(
                regularPrice: 5.00m,
                salePrice: 3.00m,
                memberPrice: 4.00m
            );

        var membership =
            CreateMembership(
                20,
                product.RetailerId
            );

        SetupEffectivePriceData(
            20,
            product,
            price,
            membership
        );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                product.Id,
                price.StoreLocationId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            3.00m,
            result.Amount
        );

        Assert.Equal(
            EffectivePriceType.Sale,
            result.PriceType
        );
    }

    [Fact]
    public async Task GetEffectivePriceAsync_WhenOnlyRegularPriceExists_ReturnsRegularPrice()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var price =
            CreatePrice(
                regularPrice: 4.29m,
                salePrice: null,
                memberPrice: null
            );

        SetupEffectivePriceData(
            20,
            product,
            price,
            membership: null
        );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                product.Id,
                price.StoreLocationId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            4.29m,
            result.Amount
        );

        Assert.Equal(
            EffectivePriceType.Regular,
            result.PriceType
        );
    }

    [Fact]
    public async Task GetEffectivePriceAsync_WhenAvailabilityIsUnavailable_ReturnsNoEffectiveAmount()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var price =
            CreatePrice(
                regularPrice: 1.00m,
                salePrice: 0.75m,
                memberPrice: 0.50m,
                availabilityStatus:
                    AvailabilityStatus.Unavailable
            );

        var membership =
            CreateMembership(
                20,
                product.RetailerId
            );

        SetupEffectivePriceData(
            20,
            product,
            price,
            membership
        );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                product.Id,
                price.StoreLocationId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            AvailabilityStatus.Unavailable,
            result.AvailabilityStatus
        );

        Assert.Null(
            result.Amount
        );

        Assert.Null(
            result.PriceType
        );
    }

    [Fact]
    public async Task GetEffectivePriceAsync_WhenAvailabilityIsUnknownButPriceExists_ReturnsEffectivePrice()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var price =
            CreatePrice(
                regularPrice: 4.29m,
                salePrice: 3.99m,
                memberPrice: null,
                availabilityStatus:
                    AvailabilityStatus.Unknown
            );

        SetupEffectivePriceData(
            20,
            product,
            price,
            membership: null
        );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                product.Id,
                price.StoreLocationId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            AvailabilityStatus.Unknown,
            result.AvailabilityStatus
        );

        Assert.Equal(
            3.99m,
            result.Amount
        );

        Assert.Equal(
            EffectivePriceType.Sale,
            result.PriceType
        );
    }

    [Fact]
    public async Task GetEffectivePriceAsync_WhenNoMonetaryPricesExist_ReturnsNoEffectiveAmount()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var price =
            CreatePrice(
                regularPrice: null,
                salePrice: null,
                memberPrice: null
            );

        SetupEffectivePriceData(
            20,
            product,
            price,
            membership: null
        );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                product.Id,
                price.StoreLocationId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Null(
            result.Amount
        );

        Assert.Null(
            result.PriceType
        );
    }

    [Fact]
    public async Task GetEffectivePriceAsync_ReturnsPriceMetadata()
    {
        // Arrange
        var product =
            CreateRetailerProduct();

        var lastCheckedAt =
            new DateTime(
                2026,
                9,
                16,
                12,
                30,
                0,
                DateTimeKind.Utc
            );

        var price =
            CreatePrice(
                regularPrice: 4.29m
            );

        price.SourceProvider =
            "KrogerPublicApi";

        price.LastCheckedAt =
            lastCheckedAt;

        SetupEffectivePriceData(
            20,
            product,
            price,
            membership: null
        );

        // Act
        var result =
            await _service.GetEffectivePriceAsync(
                20,
                product.Id,
                price.StoreLocationId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            "KrogerPublicApi",
            result.SourceProvider
        );

        Assert.Equal(
            lastCheckedAt,
            result.LastCheckedAt
        );
    }

    // ----------------------------------------------------
    // HELPERS
    // ----------------------------------------------------

    private void VerifyNoProductLookup()
    {
        _retailerProductRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    private void SetupProductAndStore(
        RetailerProduct product,
        StoreLocation store)
    {
        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    product.Id
                )
            )
            .ReturnsAsync(product);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.Id
                )
            )
            .ReturnsAsync(store);
    }

    private void SetupEffectivePriceData(
        int userId,
        RetailerProduct product,
        Price price,
        UserRetailerMembership? membership)
    {
        _priceRepositoryMock
            .Setup(repository =>
                repository.GetByProductAndStoreAsync(
                    product.Id,
                    price.StoreLocationId
                )
            )
            .ReturnsAsync(price);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    product.Id
                )
            )
            .ReturnsAsync(product);

        _membershipRepositoryMock
            .Setup(repository =>
                repository
                    .GetByUserAndRetailerAsync(
                        userId,
                        product.RetailerId
                    )
            )
            .ReturnsAsync(membership);
    }

    private static RetailerProduct
        CreateRetailerProduct(
            int id = 1,
            int retailerId = 1)
    {
        return new RetailerProduct
        {
            Id =
                id,

            ItemId =
                1,

            RetailerId =
                retailerId,

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

    private static StoreLocation
        CreateStore(
            int id = 10,
            int retailerId = 1)
    {
        return new StoreLocation
        {
            Id =
                id,

            RetailerId =
                retailerId,

            ExternalLocationId =
                "STORE-123",

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

    private static Price CreatePrice(
        decimal? regularPrice = 4.29m,
        decimal? salePrice = null,
        decimal? memberPrice = null,
        AvailabilityStatus availabilityStatus =
            AvailabilityStatus.Available)
    {
        return new Price
        {
            Id =
                100,

            RetailerProductId =
                1,

            StoreLocationId =
                10,

            RegularPrice =
                regularPrice,

            SalePrice =
                salePrice,

            MemberPrice =
                memberPrice,

            AvailabilityStatus =
                availabilityStatus,

            SourceProvider =
                "TestProvider",

            LastCheckedAt =
                DateTime.UtcNow,

            UpdatedAt =
                DateTime.UtcNow
        };
    }

    private static UserRetailerMembership
        CreateMembership(
            int userId,
            int retailerId)
    {
        return new UserRetailerMembership
        {
            Id =
                1,

            UserId =
                userId,

            RetailerId =
                retailerId,

            CreatedAt =
                DateTime.UtcNow
        };
    }
}