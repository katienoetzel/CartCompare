using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Repositories;

public class StoreLocationRepositoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_PersistsStoreLocation()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        var store =
            CreateStore(
                retailerId,
                "STORE-123"
            );

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new StoreLocationRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                store
            );
        }

        // Assert with a fresh context.
        await using var readContext =
            CreateDbContext();

        var readRepository =
            new StoreLocationRepository(
                readContext
            );

        var stored =
            await readRepository
                .GetByRetailerAndExternalIdAsync(
                    retailerId,
                    "STORE-123"
                );

        Assert.NotNull(stored);

        Assert.True(
            stored.Id > 0
        );

        Assert.Equal(
            retailerId,
            stored.RetailerId
        );

        Assert.Equal(
            "STORE-123",
            stored.ExternalLocationId
        );

        Assert.Equal(
            "Test Store",
            stored.Name
        );

        Assert.Equal(
            "123 Test Street",
            stored.AddressLine1
        );

        Assert.Equal(
            "Raleigh",
            stored.City
        );

        Assert.Equal(
            "NC",
            stored.State
        );

        Assert.Equal(
            "27606",
            stored.PostalCode
        );

        Assert.True(
            stored.IsActive
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenStoreExists_ReturnsStore()
    {
        // Arrange
        var storeId =
            await SeedStoreAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new StoreLocationRepository(
                context
            );

        // Act
        var result =
            await repository.GetByIdAsync(
                storeId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            storeId,
            result.Id
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenStoreDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context =
            CreateDbContext();

        var repository =
            new StoreLocationRepository(
                context
            );

        // Act
        var result =
            await repository.GetByIdAsync(
                999999
            );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByRetailerIdAsync_ReturnsOnlyRequestedRetailersStores()
    {
        // Arrange
        var firstRetailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        var secondRetailerId =
            await SeedRetailerAsync(
                "Harris Teeter"
            );

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new StoreLocationRepository(
                    writeContext
                );

            await writeRepository.AddAsync(
                CreateStore(
                    firstRetailerId,
                    "KROGER-1"
                )
            );

            await writeRepository.AddAsync(
                CreateStore(
                    firstRetailerId,
                    "KROGER-2"
                )
            );

            await writeRepository.AddAsync(
                CreateStore(
                    secondRetailerId,
                    "HT-1"
                )
            );
        }

        await using var readContext =
            CreateDbContext();

        var readRepository =
            new StoreLocationRepository(
                readContext
            );

        // Act
        var result =
            await readRepository
                .GetByRetailerIdAsync(
                    firstRetailerId
                );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.All(
            result,
            store =>
                Assert.Equal(
                    firstRetailerId,
                    store.RetailerId
                )
        );

        Assert.Contains(
            result,
            store =>
                store.ExternalLocationId ==
                "KROGER-1"
        );

        Assert.Contains(
            result,
            store =>
                store.ExternalLocationId ==
                "KROGER-2"
        );
    }

    [Fact]
    public async Task AddAsync_WhenRetailerAndExternalIdAreDuplicated_DatabaseRejectsDuplicate()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        await using var context =
            CreateDbContext();

        var repository =
            new StoreLocationRepository(
                context
            );

        await repository.AddAsync(
            CreateStore(
                retailerId,
                "STORE-123"
            )
        );

        var duplicate =
            CreateStore(
                retailerId,
                "STORE-123"
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    duplicate
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenDifferentRetailersUseSameExternalId_AllowsBothRows()
    {
        // Arrange
        var firstRetailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        var secondRetailerId =
            await SeedRetailerAsync(
                "Harris Teeter"
            );

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new StoreLocationRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                CreateStore(
                    firstRetailerId,
                    "STORE-123"
                )
            );

            await writeRepository.AddAsync(
                CreateStore(
                    secondRetailerId,
                    "STORE-123"
                )
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var rows =
            await readContext
                .Set<StoreLocation>()
                .Where(
                    store =>
                        store.ExternalLocationId ==
                        "STORE-123"
                )
                .ToListAsync();

        Assert.Equal(
            2,
            rows.Count
        );

        Assert.Contains(
            rows,
            store =>
                store.RetailerId ==
                firstRetailerId
        );

        Assert.Contains(
            rows,
            store =>
                store.RetailerId ==
                secondRetailerId
        );
    }

    [Fact]
    public async Task UpdateAsync_PersistsStoreChanges()
    {
        // Arrange
        var storeId =
            await SeedStoreAsync();

        await using (
            var updateContext =
                CreateDbContext())
        {
            var updateRepository =
                new StoreLocationRepository(
                    updateContext
                );

            var store =
                await updateRepository
                    .GetByIdAsync(
                        storeId
                    );

            Assert.NotNull(store);

            store.Name =
                "Updated Store";

            store.AddressLine1 =
                "500 Updated Street";

            store.City =
                "Cary";

            store.PostalCode =
                "27513";

            store.Latitude =
                35.79m;

            store.Longitude =
                -78.78m;

            store.IsActive =
                false;

            store.LastSeenAt =
                DateTime.UtcNow;

            // Act
            await updateRepository.UpdateAsync(
                store
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var stored =
            await readContext
                .Set<StoreLocation>()
                .SingleAsync(
                    store =>
                        store.Id ==
                        storeId
                );

        Assert.Equal(
            "Updated Store",
            stored.Name
        );

        Assert.Equal(
            "500 Updated Street",
            stored.AddressLine1
        );

        Assert.Equal(
            "Cary",
            stored.City
        );

        Assert.Equal(
            "27513",
            stored.PostalCode
        );

        Assert.Equal(
            35.79m,
            stored.Latitude
        );

        Assert.Equal(
            -78.78m,
            stored.Longitude
        );

        Assert.False(
            stored.IsActive
        );
    }

    [Fact]
    public async Task AddAsync_WhenRetailerDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        await using var context =
            CreateDbContext();

        var repository =
            new StoreLocationRepository(
                context
            );

        var store =
            CreateStore(
                retailerId: 999999,
                externalLocationId:
                    "STORE-123"
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    store
                )
        );
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    private async Task<int>
        SeedRetailerAsync(
            string name)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            new Retailer
            {
                Name = name,

                SupportsMembership =
                    true,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        return retailer.Id;
    }

    private async Task<int>
        SeedStoreAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            new Retailer
            {
                Name =
                    "Kroger",

                SupportsMembership =
                    true,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        var store =
            CreateStore(
                retailer.Id,
                "STORE-123"
            );

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return store.Id;
    }

    private static StoreLocation
        CreateStore(
            int retailerId,
            string externalLocationId)
    {
        return new StoreLocation
        {
            RetailerId =
                retailerId,

            ExternalLocationId =
                externalLocationId,

            Name =
                "Test Store",

            AddressLine1 =
                "123 Test Street",

            AddressLine2 =
                null,

            City =
                "Raleigh",

            State =
                "NC",

            PostalCode =
                "27606",

            Latitude =
                35.78m,

            Longitude =
                -78.64m,

            IsActive =
                true,

            LastSeenAt =
                DateTime.UtcNow
        };
    }
}