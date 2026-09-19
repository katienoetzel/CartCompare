using CartCompare.Entities;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class StoreLocationSyncServiceTests
{
    private readonly Mock<IRetailerRepository>
        _retailerRepositoryMock;

    private readonly Mock<IStoreLocationRepository>
        _storeLocationRepositoryMock;

    private readonly Mock<IPriceProviderResolver>
        _providerResolverMock;

    private readonly Mock<IPriceProvider>
        _providerMock;

    private readonly StoreLocationSyncService
        _service;

    public StoreLocationSyncServiceTests()
    {
        _retailerRepositoryMock =
            new Mock<IRetailerRepository>();

        _storeLocationRepositoryMock =
            new Mock<IStoreLocationRepository>();

        _providerResolverMock =
            new Mock<IPriceProviderResolver>();

        _providerMock =
            new Mock<IPriceProvider>();

        _service =
            new StoreLocationSyncService(
                _retailerRepositoryMock.Object,
                _storeLocationRepositoryMock.Object,
                _providerResolverMock.Object
            );
    }

    [Fact]
    public async Task SyncAsync_WhenRetailerDoesNotExist_ReturnsRetailerNotFound()
    {
        // Arrange
        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(999)
            )
            .ReturnsAsync(
                (Retailer?)null
            );

        // Act
        var result =
            await _service.SyncAsync(
                retailerId: 999,
                postalCode: "27606"
            );

        // Assert
        Assert.Equal(
            StoreLocationSyncResultType.RetailerNotFound,
            result.Result
        );

        Assert.Equal(
            999,
            result.RetailerId
        );

        Assert.Equal(
            "Unknown",
            result.RetailerName
        );

        _providerResolverMock.Verify(
            resolver =>
                resolver.Resolve(
                    It.IsAny<string>()
                ),
            Times.Never
        );

        _providerMock.Verify(
            provider =>
                provider.FindStoresAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenProviderDoesNotExist_ReturnsProviderNotFound()
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
                retailer.Id,
                "27606"
            );

        // Assert
        Assert.Equal(
            StoreLocationSyncResultType.ProviderNotFound,
            result.Result
        );

        Assert.Equal(
            retailer.Id,
            result.RetailerId
        );

        Assert.Equal(
            retailer.Name,
            result.RetailerName
        );

        Assert.Null(
            result.ProviderName
        );

        _providerMock.Verify(
            provider =>
                provider.FindStoresAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_TrimsPostalCodeBeforeCallingProvider()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        SetupRetailerAndProvider(
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.FindStoresAsync(
                    retailer.Name,
                    "27606"
                )
            )
            .ReturnsAsync(
                new List<ProviderStoreLocation>()
            );

        // Act
        await _service.SyncAsync(
            retailer.Id,
            "  27606  "
        );

        // Assert
        _providerMock.Verify(
            provider =>
                provider.FindStoresAsync(
                    retailer.Name,
                    "27606"
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task SyncAsync_WhenProviderReturnsNoStores_ReturnsSuccessfulZeroCounts()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        SetupRetailerAndProvider(
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.FindStoresAsync(
                    retailer.Name,
                    "27606"
                )
            )
            .ReturnsAsync(
                new List<ProviderStoreLocation>()
            );

        // Act
        var result =
            await _service.SyncAsync(
                retailer.Id,
                "27606"
            );

        // Assert
        Assert.Equal(
            StoreLocationSyncResultType.Succeeded,
            result.Result
        );

        Assert.Equal(
            retailer.Id,
            result.RetailerId
        );

        Assert.Equal(
            retailer.Name,
            result.RetailerName
        );

        Assert.Equal(
            "KrogerPublicApi",
            result.ProviderName
        );

        Assert.Equal(
            0,
            result.ProviderStoreCount
        );

        Assert.Equal(
            0,
            result.CreatedCount
        );

        Assert.Equal(
            0,
            result.UpdatedCount
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<StoreLocation>()
                ),
            Times.Never
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    It.IsAny<StoreLocation>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenStoresAreNew_CreatesStoreLocations()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        var firstProviderStore =
            CreateProviderStore(
                externalLocationId: "STORE-1",
                name: "Kroger One",
                addressLine1: "100 Main Street",
                city: "Raleigh",
                postalCode: "27606"
            );

        var secondProviderStore =
            CreateProviderStore(
                externalLocationId: "STORE-2",
                name: "Kroger Two",
                addressLine1: "200 Main Street",
                city: "Raleigh",
                postalCode: "27607"
            );

        SetupRetailerAndProvider(
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.FindStoresAsync(
                    retailer.Name,
                    "27606"
                )
            )
            .ReturnsAsync(
                new List<ProviderStoreLocation>
                {
                    firstProviderStore,
                    secondProviderStore
                }
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "STORE-1"
                    )
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "STORE-2"
                    )
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

        var addedStores =
            new List<StoreLocation>();

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<StoreLocation>()
                )
            )
            .Callback<StoreLocation>(
                store =>
                    addedStores.Add(store)
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.SyncAsync(
                retailer.Id,
                "27606"
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.Equal(
            StoreLocationSyncResultType.Succeeded,
            result.Result
        );

        Assert.Equal(
            2,
            result.ProviderStoreCount
        );

        Assert.Equal(
            2,
            result.CreatedCount
        );

        Assert.Equal(
            0,
            result.UpdatedCount
        );

        Assert.Equal(
            2,
            addedStores.Count
        );

        var firstAddedStore =
            addedStores.Single(
                store =>
                    store.ExternalLocationId ==
                    "STORE-1"
            );

        Assert.Equal(
            retailer.Id,
            firstAddedStore.RetailerId
        );

        Assert.Equal(
            "Kroger One",
            firstAddedStore.Name
        );

        Assert.Equal(
            "100 Main Street",
            firstAddedStore.AddressLine1
        );

        Assert.Equal(
            "Raleigh",
            firstAddedStore.City
        );

        Assert.Equal(
            "NC",
            firstAddedStore.State
        );

        Assert.Equal(
            "27606",
            firstAddedStore.PostalCode
        );

        Assert.Equal(
            35.78m,
            firstAddedStore.Latitude
        );

        Assert.Equal(
            -78.64m,
            firstAddedStore.Longitude
        );

        Assert.True(
            firstAddedStore.IsActive
        );

        Assert.InRange(
            firstAddedStore.LastSeenAt,
            before,
            after
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<StoreLocation>()
                ),
            Times.Exactly(2)
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    It.IsAny<StoreLocation>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_WhenStoreAlreadyExists_RefreshesAndReactivatesStore()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        var existingStore =
            new StoreLocation
            {
                Id = 10,
                RetailerId = retailer.Id,

                ExternalLocationId =
                    "STORE-1",

                Name =
                    "Old Name",

                AddressLine1 =
                    "Old Address",

                AddressLine2 =
                    null,

                City =
                    "Old City",

                State =
                    "NC",

                PostalCode =
                    "00000",

                Latitude =
                    null,

                Longitude =
                    null,

                IsActive =
                    false,

                LastSeenAt =
                    DateTime.UtcNow.AddDays(-30)
            };

        var providerStore =
            CreateProviderStore(
                externalLocationId:
                    "STORE-1",

                name:
                    "Updated Kroger",

                addressLine1:
                    "500 New Street",

                city:
                    "Raleigh",

                postalCode:
                    "27606"
            );

        providerStore.AddressLine2 =
            "Suite 200";

        providerStore.Latitude =
            35.81m;

        providerStore.Longitude =
            -78.62m;

        SetupRetailerAndProvider(
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.FindStoresAsync(
                    retailer.Name,
                    "27606"
                )
            )
            .ReturnsAsync(
                new List<ProviderStoreLocation>
                {
                    providerStore
                }
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        providerStore.ExternalLocationId
                    )
            )
            .ReturnsAsync(
                existingStore
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.UpdateAsync(
                    existingStore
                )
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.SyncAsync(
                retailer.Id,
                "27606"
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.Equal(
            StoreLocationSyncResultType.Succeeded,
            result.Result
        );

        Assert.Equal(
            1,
            result.ProviderStoreCount
        );

        Assert.Equal(
            0,
            result.CreatedCount
        );

        Assert.Equal(
            1,
            result.UpdatedCount
        );

        Assert.Equal(
            "Updated Kroger",
            existingStore.Name
        );

        Assert.Equal(
            "500 New Street",
            existingStore.AddressLine1
        );

        Assert.Equal(
            "Suite 200",
            existingStore.AddressLine2
        );

        Assert.Equal(
            "Raleigh",
            existingStore.City
        );

        Assert.Equal(
            "NC",
            existingStore.State
        );

        Assert.Equal(
            "27606",
            existingStore.PostalCode
        );

        Assert.Equal(
            35.81m,
            existingStore.Latitude
        );

        Assert.Equal(
            -78.62m,
            existingStore.Longitude
        );

        Assert.True(
            existingStore.IsActive
        );

        Assert.InRange(
            existingStore.LastSeenAt,
            before,
            after
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    existingStore
                ),
            Times.Once
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
    public async Task SyncAsync_WhenResultsContainNewAndExistingStores_TracksCountsSeparately()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        var newProviderStore =
            CreateProviderStore(
                externalLocationId:
                    "NEW-STORE",

                name:
                    "New Kroger",

                addressLine1:
                    "100 New Street",

                city:
                    "Raleigh",

                postalCode:
                    "27606"
            );

        var existingProviderStore =
            CreateProviderStore(
                externalLocationId:
                    "EXISTING-STORE",

                name:
                    "Existing Kroger",

                addressLine1:
                    "200 Existing Street",

                city:
                    "Raleigh",

                postalCode:
                    "27606"
            );

        var existingStore =
            new StoreLocation
            {
                Id = 50,
                RetailerId = retailer.Id,

                ExternalLocationId =
                    "EXISTING-STORE",

                Name =
                    "Old Existing Store",

                AddressLine1 =
                    "Old Address",

                City =
                    "Raleigh",

                State =
                    "NC",

                PostalCode =
                    "27606",

                IsActive =
                    true,

                LastSeenAt =
                    DateTime.UtcNow.AddDays(-1)
            };

        SetupRetailerAndProvider(
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.FindStoresAsync(
                    retailer.Name,
                    "27606"
                )
            )
            .ReturnsAsync(
                new List<ProviderStoreLocation>
                {
                    newProviderStore,
                    existingProviderStore
                }
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "NEW-STORE"
                    )
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "EXISTING-STORE"
                    )
            )
            .ReturnsAsync(
                existingStore
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<StoreLocation>()
                )
            )
            .Returns(Task.CompletedTask);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.UpdateAsync(
                    existingStore
                )
            )
            .Returns(Task.CompletedTask);

        // Act
        var result =
            await _service.SyncAsync(
                retailer.Id,
                "27606"
            );

        // Assert
        Assert.Equal(
            2,
            result.ProviderStoreCount
        );

        Assert.Equal(
            1,
            result.CreatedCount
        );

        Assert.Equal(
            1,
            result.UpdatedCount
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<StoreLocation>()
                ),
            Times.Once
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    existingStore
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task SyncAsync_DoesNotDeactivateStoresMissingFromLocalProviderSearch()
    {
        // Arrange
        var retailer =
            CreateRetailer();

        var returnedProviderStore =
            CreateProviderStore(
                externalLocationId:
                    "RETURNED-STORE",

                name:
                    "Returned Kroger",

                addressLine1:
                    "100 Main Street",

                city:
                    "Raleigh",

                postalCode:
                    "27606"
            );

        var returnedExistingStore =
            new StoreLocation
            {
                Id = 10,
                RetailerId = retailer.Id,

                ExternalLocationId =
                    "RETURNED-STORE",

                Name =
                    "Old Returned Store",

                AddressLine1 =
                    "Old Address",

                City =
                    "Raleigh",

                State =
                    "NC",

                PostalCode =
                    "27606",

                IsActive =
                    true,

                LastSeenAt =
                    DateTime.UtcNow.AddDays(-1)
            };

        var storeNotReturnedBySearch =
            new StoreLocation
            {
                Id = 11,
                RetailerId = retailer.Id,

                ExternalLocationId =
                    "NOT-RETURNED",

                Name =
                    "Another Kroger",

                AddressLine1 =
                    "200 Other Street",

                City =
                    "Raleigh",

                State =
                    "NC",

                PostalCode =
                    "27607",

                IsActive =
                    true,

                LastSeenAt =
                    DateTime.UtcNow.AddDays(-1)
            };

        SetupRetailerAndProvider(
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.FindStoresAsync(
                    retailer.Name,
                    "27606"
                )
            )
            .ReturnsAsync(
                new List<ProviderStoreLocation>
                {
                    returnedProviderStore
                }
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        "RETURNED-STORE"
                    )
            )
            .ReturnsAsync(
                returnedExistingStore
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.UpdateAsync(
                    returnedExistingStore
                )
            )
            .Returns(Task.CompletedTask);

        // Act
        await _service.SyncAsync(
            retailer.Id,
            "27606"
        );

        // Assert
        Assert.True(
            storeNotReturnedBySearch.IsActive
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    storeNotReturnedBySearch
                ),
            Times.Never
        );
    }

    private void SetupRetailerAndProvider(
        Retailer retailer)
    {
        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    retailer.Id
                )
            )
            .ReturnsAsync(retailer);

        _providerResolverMock
            .Setup(resolver =>
                resolver.Resolve(
                    retailer.Name
                )
            )
            .Returns(
                _providerMock.Object
            );

        _providerMock
            .SetupGet(provider =>
                provider.ProviderName
            )
            .Returns(
                "KrogerPublicApi"
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

    private static ProviderStoreLocation
        CreateProviderStore(
            string externalLocationId,
            string name,
            string addressLine1,
            string city,
            string postalCode)
    {
        return new ProviderStoreLocation
        {
            ExternalLocationId =
                externalLocationId,

            Name =
                name,

            AddressLine1 =
                addressLine1,

            AddressLine2 =
                null,

            City =
                city,

            State =
                "NC",

            PostalCode =
                postalCode,

            Latitude =
                35.78m,

            Longitude =
                -78.64m
        };
    }
}