using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class StoreLocationServiceTests
{
    private readonly Mock<IStoreLocationRepository>
        _storeLocationRepositoryMock;

    private readonly Mock<IRetailerRepository>
        _retailerRepositoryMock;

    private readonly StoreLocationService
        _service;

    public StoreLocationServiceTests()
    {
        _storeLocationRepositoryMock =
            new Mock<IStoreLocationRepository>();

        _retailerRepositoryMock =
            new Mock<IRetailerRepository>();

        _service =
            new StoreLocationService(
                _storeLocationRepositoryMock.Object,
                _retailerRepositoryMock.Object
            );
    }

    [Fact]
    public async Task GetByRetailerIdAsync_ReturnsRepositoryResults()
    {
        // Arrange
        var stores =
            new List<StoreLocation>
            {
                CreateStore(
                    id: 1,
                    externalLocationId: "STORE-1"
                ),

                CreateStore(
                    id: 2,
                    externalLocationId: "STORE-2"
                )
            };

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByRetailerIdAsync(
                    1
                )
            )
            .ReturnsAsync(stores);

        // Act
        var result =
            await _service.GetByRetailerIdAsync(
                1
            );

        // Assert
        Assert.Same(
            stores,
            result
        );

        Assert.Equal(
            2,
            result.Count
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenStoreExists_ReturnsStore()
    {
        // Arrange
        var store =
            CreateStore(
                id: 10,
                externalLocationId: "STORE-10"
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.Id
                )
            )
            .ReturnsAsync(store);

        // Act
        var result =
            await _service.GetByIdAsync(
                store.Id
            );

        // Assert
        Assert.Same(
            store,
            result
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenStoreDoesNotExist_ReturnsNull()
    {
        // Arrange
        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    999
                )
            )
            .ReturnsAsync(
                (StoreLocation?)null
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
                retailerId: 999,
                externalLocationId: "STORE-123",
                name: "Test Store",
                addressLine1: "123 Main Street",
                addressLine2: null,
                city: "Raleigh",
                state: "NC",
                postalCode: "27606",
                latitude: 35.78m,
                longitude: -78.67m
            );

        // Assert
        Assert.Equal(
            StoreLocationCreateResult
                .RetailerNotFound,
            result.Result
        );

        Assert.Null(
            result.StoreLocation
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        It.IsAny<int>(),
                        It.IsAny<string>()
                    ),
            Times.Never
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<StoreLocation>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateAsync_WhenStoreAlreadyExists_ReturnsAlreadyExists()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        var existingStore =
            CreateStore(
                id: 10,
                externalLocationId:
                    "STORE-123"
            );

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    retailer.Id
                )
            )
            .ReturnsAsync(retailer);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "STORE-123"
                    )
            )
            .ReturnsAsync(
                existingStore
            );

        // Act
        var result =
            await _service.CreateAsync(
                retailer.Id,
                "  STORE-123  ",
                "Test Store",
                "123 Main Street",
                null,
                "Raleigh",
                "NC",
                "27606",
                null,
                null
            );

        // Assert
        Assert.Equal(
            StoreLocationCreateResult
                .AlreadyExists,
            result.Result
        );

        Assert.Same(
            existingStore,
            result.StoreLocation
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<StoreLocation>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesTrimmedActiveStore()
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

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "STORE-123"
                    )
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

        StoreLocation? addedStore =
            null;

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<StoreLocation>()
                )
            )
            .Callback<StoreLocation>(
                store =>
                {
                    store.Id = 50;
                    addedStore = store;
                }
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.CreateAsync(
                retailer.Id,
                "  STORE-123  ",
                "  Raleigh Kroger  ",
                "  123 Main Street  ",
                "  Suite A  ",
                "  Raleigh  ",
                "  NC  ",
                "  27606  ",
                35.78m,
                -78.67m
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.Equal(
            StoreLocationCreateResult.Created,
            result.Result
        );

        Assert.NotNull(
            addedStore
        );

        Assert.Equal(
            retailer.Id,
            addedStore.RetailerId
        );

        Assert.Equal(
            "STORE-123",
            addedStore.ExternalLocationId
        );

        Assert.Equal(
            "Raleigh Kroger",
            addedStore.Name
        );

        Assert.Equal(
            "123 Main Street",
            addedStore.AddressLine1
        );

        Assert.Equal(
            "Suite A",
            addedStore.AddressLine2
        );

        Assert.Equal(
            "Raleigh",
            addedStore.City
        );

        Assert.Equal(
            "NC",
            addedStore.State
        );

        Assert.Equal(
            "27606",
            addedStore.PostalCode
        );

        Assert.Equal(
            35.78m,
            addedStore.Latitude
        );

        Assert.Equal(
            -78.67m,
            addedStore.Longitude
        );

        Assert.True(
            addedStore.IsActive
        );

        Assert.InRange(
            addedStore.LastSeenAt,
            before,
            after
        );

        Assert.Same(
            addedStore,
            result.StoreLocation
        );
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

    private static StoreLocation CreateStore(
        int id,
        string externalLocationId)
    {
        return new StoreLocation
        {
            Id = id,
            RetailerId = 1,
            ExternalLocationId =
                externalLocationId,
            Name = "Test Store",
            AddressLine1 =
                "123 Main Street",
            City = "Raleigh",
            State = "NC",
            PostalCode = "27606",
            IsActive = true,
            LastSeenAt = DateTime.UtcNow
        };
    }
}