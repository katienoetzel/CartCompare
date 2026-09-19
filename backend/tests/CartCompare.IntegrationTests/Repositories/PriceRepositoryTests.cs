using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Repositories;

public class PriceRepositoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_PersistsPrice()
    {
        // Arrange
        var seeded =
            await SeedProductAndStoreAsync();

        var price =
            CreatePrice(
                seeded.RetailerProductId,
                seeded.StoreLocationId,
                regularPrice: 4.29m,
                salePrice: 3.99m,
                memberPrice: 3.49m
            );

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new PriceRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                price
            );
        }

        // Assert using a new DbContext.
        await using var readContext =
            CreateDbContext();

        var readRepository =
            new PriceRepository(
                readContext
            );

        var stored =
            await readRepository
                .GetByProductAndStoreAsync(
                    seeded.RetailerProductId,
                    seeded.StoreLocationId
                );

        Assert.NotNull(stored);

        Assert.True(
            stored.Id > 0
        );

        Assert.Equal(
            4.29m,
            stored.RegularPrice
        );

        Assert.Equal(
            3.99m,
            stored.SalePrice
        );

        Assert.Equal(
            3.49m,
            stored.MemberPrice
        );

        Assert.Equal(
            AvailabilityStatus.Available,
            stored.AvailabilityStatus
        );

        Assert.Equal(
            "TestProvider",
            stored.SourceProvider
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenPriceExists_ReturnsPrice()
    {
        // Arrange
        var seeded =
            await SeedProductAndStoreAsync();

        int priceId;

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new PriceRepository(
                    writeContext
                );

            var price =
                CreatePrice(
                    seeded.RetailerProductId,
                    seeded.StoreLocationId
                );

            await writeRepository.AddAsync(
                price
            );

            priceId =
                price.Id;
        }

        await using var readContext =
            CreateDbContext();

        var readRepository =
            new PriceRepository(
                readContext
            );

        // Act
        var result =
            await readRepository.GetByIdAsync(
                priceId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            priceId,
            result.Id
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenPriceDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context =
            CreateDbContext();

        var repository =
            new PriceRepository(
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
    public async Task GetByProductAndStoreAsync_WhenPriceDoesNotExist_ReturnsNull()
    {
        // Arrange
        var seeded =
            await SeedProductAndStoreAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new PriceRepository(
                context
            );

        // Act
        var result =
            await repository
                .GetByProductAndStoreAsync(
                    seeded.RetailerProductId,
                    seeded.StoreLocationId
                );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByRetailerProductIdAsync_ReturnsOnlyPricesForRequestedProduct()
    {
        // Arrange
        var data =
            await SeedTwoProductsAndTwoStoresAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new PriceRepository(
                    writeContext
                );

            await writeRepository.AddAsync(
                CreatePrice(
                    data.FirstProductId,
                    data.FirstStoreId,
                    regularPrice: 4.00m
                )
            );

            await writeRepository.AddAsync(
                CreatePrice(
                    data.FirstProductId,
                    data.SecondStoreId,
                    regularPrice: 5.00m
                )
            );

            await writeRepository.AddAsync(
                CreatePrice(
                    data.SecondProductId,
                    data.FirstStoreId,
                    regularPrice: 6.00m
                )
            );
        }

        await using var readContext =
            CreateDbContext();

        var readRepository =
            new PriceRepository(
                readContext
            );

        // Act
        var result =
            await readRepository
                .GetByRetailerProductIdAsync(
                    data.FirstProductId
                );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.All(
            result,
            price =>
                Assert.Equal(
                    data.FirstProductId,
                    price.RetailerProductId
                )
        );

        Assert.Contains(
            result,
            price =>
                price.StoreLocationId ==
                data.FirstStoreId
        );

        Assert.Contains(
            result,
            price =>
                price.StoreLocationId ==
                data.SecondStoreId
        );
    }

    [Fact]
    public async Task AddAsync_WhenSameProductAndStoreAreAddedTwice_DatabaseRejectsDuplicate()
    {
        // Arrange
        var seeded =
            await SeedProductAndStoreAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new PriceRepository(
                context
            );

        await repository.AddAsync(
            CreatePrice(
                seeded.RetailerProductId,
                seeded.StoreLocationId,
                regularPrice: 4.00m
            )
        );

        var duplicate =
            CreatePrice(
                seeded.RetailerProductId,
                seeded.StoreLocationId,
                regularPrice: 5.00m
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
    public async Task AddAsync_WhenSameProductUsesDifferentStores_AllowsBothRows()
    {
        // Arrange
        var data =
            await SeedOneProductAndTwoStoresAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new PriceRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                CreatePrice(
                    data.ProductId,
                    data.FirstStoreId,
                    regularPrice: 4.00m
                )
            );

            await writeRepository.AddAsync(
                CreatePrice(
                    data.ProductId,
                    data.SecondStoreId,
                    regularPrice: 5.00m
                )
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var prices =
            await readContext
                .Set<Price>()
                .Where(
                    price =>
                        price.RetailerProductId ==
                        data.ProductId
                )
                .ToListAsync();

        Assert.Equal(
            2,
            prices.Count
        );
    }

    [Fact]
    public async Task AddAsync_WhenDifferentProductsUseSameStore_AllowsBothRows()
    {
        // Arrange
        var data =
            await SeedTwoProductsAndOneStoreAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new PriceRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                CreatePrice(
                    data.FirstProductId,
                    data.StoreId,
                    regularPrice: 4.00m
                )
            );

            await writeRepository.AddAsync(
                CreatePrice(
                    data.SecondProductId,
                    data.StoreId,
                    regularPrice: 6.00m
                )
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var prices =
            await readContext
                .Set<Price>()
                .Where(
                    price =>
                        price.StoreLocationId ==
                        data.StoreId
                )
                .ToListAsync();

        Assert.Equal(
            2,
            prices.Count
        );
    }

    [Fact]
    public async Task AddAsync_AllowsNullableMoneyFields()
    {
        // Arrange
        var seeded =
            await SeedProductAndStoreAsync();

        var price =
            CreatePrice(
                seeded.RetailerProductId,
                seeded.StoreLocationId,
                regularPrice: null,
                salePrice: null,
                memberPrice: null,
                availabilityStatus:
                    AvailabilityStatus.Unknown
            );

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new PriceRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                price
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var stored =
            await readContext
                .Set<Price>()
                .SingleAsync();

        Assert.Null(
            stored.RegularPrice
        );

        Assert.Null(
            stored.SalePrice
        );

        Assert.Null(
            stored.MemberPrice
        );

        Assert.Equal(
            AvailabilityStatus.Unknown,
            stored.AvailabilityStatus
        );
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangedPriceValues()
    {
        // Arrange
        var seeded =
            await SeedProductAndStoreAsync();

        int priceId;

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new PriceRepository(
                    writeContext
                );

            var price =
                CreatePrice(
                    seeded.RetailerProductId,
                    seeded.StoreLocationId,
                    regularPrice: 5.00m
                );

            await writeRepository.AddAsync(
                price
            );

            priceId =
                price.Id;
        }

        await using (
            var updateContext =
                CreateDbContext())
        {
            var updateRepository =
                new PriceRepository(
                    updateContext
                );

            var price =
                await updateRepository
                    .GetByIdAsync(
                        priceId
                    );

            Assert.NotNull(price);

            price.RegularPrice =
                4.50m;

            price.SalePrice =
                3.99m;

            price.MemberPrice =
                3.49m;

            price.AvailabilityStatus =
                AvailabilityStatus.Unknown;

            price.SourceProvider =
                "UpdatedProvider";

            price.SourceUpdatedAt =
                DateTime.UtcNow.AddMinutes(-5);

            price.LastCheckedAt =
                DateTime.UtcNow;

            price.UpdatedAt =
                DateTime.UtcNow;

            // Act
            await updateRepository.UpdateAsync(
                price
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var stored =
            await readContext
                .Set<Price>()
                .SingleAsync(
                    price =>
                        price.Id ==
                        priceId
                );

        Assert.Equal(
            4.50m,
            stored.RegularPrice
        );

        Assert.Equal(
            3.99m,
            stored.SalePrice
        );

        Assert.Equal(
            3.49m,
            stored.MemberPrice
        );

        Assert.Equal(
            AvailabilityStatus.Unknown,
            stored.AvailabilityStatus
        );

        Assert.Equal(
            "UpdatedProvider",
            stored.SourceProvider
        );
    }

    [Fact]
    public async Task AddAsync_WhenRetailerProductDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var storeId =
            await SeedStoreAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new PriceRepository(
                context
            );

        var price =
            CreatePrice(
                retailerProductId: 999999,
                storeLocationId: storeId
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    price
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenStoreDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var productId =
            await SeedRetailerProductAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new PriceRepository(
                context
            );

        var price =
            CreatePrice(
                retailerProductId: productId,
                storeLocationId: 999999
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    price
                )
        );
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    private static Price CreatePrice(
        int retailerProductId,
        int storeLocationId,
        decimal? regularPrice = 4.29m,
        decimal? salePrice = null,
        decimal? memberPrice = null,
        AvailabilityStatus availabilityStatus =
            AvailabilityStatus.Available)
    {
        var now =
            DateTime.UtcNow;

        return new Price
        {
            RetailerProductId =
                retailerProductId,

            StoreLocationId =
                storeLocationId,

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

            SourceUpdatedAt =
                null,

            LastCheckedAt =
                now,

            UpdatedAt =
                now
        };
    }

    private async Task<(
        int RetailerProductId,
        int StoreLocationId)>
        SeedProductAndStoreAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer();

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var product =
            CreateRetailerProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        var store =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return (
            product.Id,
            store.Id
        );
    }

    private async Task<int>
        SeedStoreAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer();

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        var store =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return store.Id;
    }

    private async Task<int>
        SeedRetailerProductAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer();

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var product =
            CreateRetailerProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        await context.SaveChangesAsync();

        return product.Id;
    }

    private async Task<(
        int ProductId,
        int FirstStoreId,
        int SecondStoreId)>
        SeedOneProductAndTwoStoresAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer();

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        var product =
            CreateRetailerProduct(
                item.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        var firstStore =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        var secondStore =
            CreateStore(
                retailer.Id,
                "STORE-2"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        context.Set<StoreLocation>()
            .AddRange(
                firstStore,
                secondStore
            );

        await context.SaveChangesAsync();

        return (
            product.Id,
            firstStore.Id,
            secondStore.Id
        );
    }

    private async Task<(
        int FirstProductId,
        int SecondProductId,
        int StoreId)>
        SeedTwoProductsAndOneStoreAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer();

        var firstItem =
            CreateItem(
                "Whole Milk"
            );

        var secondItem =
            CreateItem(
                "Large Eggs"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .AddRange(
                firstItem,
                secondItem
            );

        await context.SaveChangesAsync();

        var firstProduct =
            CreateRetailerProduct(
                firstItem.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        var secondProduct =
            CreateRetailerProduct(
                secondItem.Id,
                retailer.Id,
                "PRODUCT-2"
            );

        var store =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        context.Set<RetailerProduct>()
            .AddRange(
                firstProduct,
                secondProduct
            );

        context.Set<StoreLocation>()
            .Add(store);

        await context.SaveChangesAsync();

        return (
            firstProduct.Id,
            secondProduct.Id,
            store.Id
        );
    }

    private async Task<(
        int FirstProductId,
        int SecondProductId,
        int FirstStoreId,
        int SecondStoreId)>
        SeedTwoProductsAndTwoStoresAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer();

        var firstItem =
            CreateItem(
                "Whole Milk"
            );

        var secondItem =
            CreateItem(
                "Large Eggs"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .AddRange(
                firstItem,
                secondItem
            );

        await context.SaveChangesAsync();

        var firstProduct =
            CreateRetailerProduct(
                firstItem.Id,
                retailer.Id,
                "PRODUCT-1"
            );

        var secondProduct =
            CreateRetailerProduct(
                secondItem.Id,
                retailer.Id,
                "PRODUCT-2"
            );

        var firstStore =
            CreateStore(
                retailer.Id,
                "STORE-1"
            );

        var secondStore =
            CreateStore(
                retailer.Id,
                "STORE-2"
            );

        context.Set<RetailerProduct>()
            .AddRange(
                firstProduct,
                secondProduct
            );

        context.Set<StoreLocation>()
            .AddRange(
                firstStore,
                secondStore
            );

        await context.SaveChangesAsync();

        return (
            firstProduct.Id,
            secondProduct.Id,
            firstStore.Id,
            secondStore.Id
        );
    }

    private static Retailer
        CreateRetailer()
    {
        return new Retailer
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
    }

    private static Item CreateItem(
        string name)
    {
        var now =
            DateTime.UtcNow;

        return new Item
        {
            Name = name,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static RetailerProduct
        CreateRetailerProduct(
            int itemId,
            int retailerId,
            string externalProductId)
    {
        return new RetailerProduct
        {
            ItemId =
                itemId,

            RetailerId =
                retailerId,

            ExternalProductId =
                externalProductId,

            Name =
                "Test Product",

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

            City =
                "Raleigh",

            State =
                "NC",

            PostalCode =
                "27606",

            IsActive =
                true,

            LastSeenAt =
                DateTime.UtcNow
        };
    }
}