using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Repositories;

public class RetailerProductRepositoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_PersistsRetailerProduct()
    {
        // Arrange
        var seeded =
            await SeedRetailerAndItemAsync();

        var product =
            CreateRetailerProduct(
                seeded.ItemId,
                seeded.RetailerId,
                "PRODUCT-123"
            );

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new RetailerProductRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                product
            );
        }

        // Assert with a fresh DbContext.
        await using var readContext =
            CreateDbContext();

        var readRepository =
            new RetailerProductRepository(
                readContext
            );

        var stored =
            await readRepository
                .GetByRetailerAndExternalIdAsync(
                    seeded.RetailerId,
                    "PRODUCT-123"
                );

        Assert.NotNull(stored);

        Assert.True(
            stored.Id > 0
        );

        Assert.Equal(
            seeded.ItemId,
            stored.ItemId
        );

        Assert.Equal(
            seeded.RetailerId,
            stored.RetailerId
        );

        Assert.Equal(
            "PRODUCT-123",
            stored.ExternalProductId
        );

        Assert.Equal(
            "Test Product",
            stored.Name
        );

        Assert.Equal(
            "Test Brand",
            stored.Brand
        );

        Assert.Equal(
            "12 oz",
            stored.Size
        );

        Assert.Equal(
            "012345678905",
            stored.Upc
        );

        Assert.Equal(
            ProductMatchMethod.Manual,
            stored.MatchMethod
        );

        Assert.True(
            stored.IsActive
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductExists_ReturnsProduct()
    {
        // Arrange
        var productId =
            await SeedRetailerProductAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new RetailerProductRepository(
                context
            );

        // Act
        var result =
            await repository.GetByIdAsync(
                productId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            productId,
            result.Id
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context =
            CreateDbContext();

        var repository =
            new RetailerProductRepository(
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
    public async Task GetByRetailerIdAsync_ReturnsOnlyRequestedRetailersProducts()
    {
        // Arrange
        var data =
            await SeedTwoRetailersAndTwoItemsAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new RetailerProductRepository(
                    writeContext
                );

            await writeRepository.AddAsync(
                CreateRetailerProduct(
                    data.FirstItemId,
                    data.FirstRetailerId,
                    "KROGER-1"
                )
            );

            await writeRepository.AddAsync(
                CreateRetailerProduct(
                    data.SecondItemId,
                    data.FirstRetailerId,
                    "KROGER-2"
                )
            );

            await writeRepository.AddAsync(
                CreateRetailerProduct(
                    data.FirstItemId,
                    data.SecondRetailerId,
                    "HT-1"
                )
            );
        }

        await using var readContext =
            CreateDbContext();

        var readRepository =
            new RetailerProductRepository(
                readContext
            );

        // Act
        var result =
            await readRepository
                .GetByRetailerIdAsync(
                    data.FirstRetailerId
                );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.All(
            result,
            product =>
                Assert.Equal(
                    data.FirstRetailerId,
                    product.RetailerId
                )
        );

        Assert.Contains(
            result,
            product =>
                product.ExternalProductId ==
                "KROGER-1"
        );

        Assert.Contains(
            result,
            product =>
                product.ExternalProductId ==
                "KROGER-2"
        );
    }

    [Fact]
    public async Task GetByItemIdAsync_ReturnsOnlyProductsForRequestedItem()
    {
        // Arrange
        var data =
            await SeedTwoRetailersAndTwoItemsAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new RetailerProductRepository(
                    writeContext
                );

            await writeRepository.AddAsync(
                CreateRetailerProduct(
                    data.FirstItemId,
                    data.FirstRetailerId,
                    "PRODUCT-1"
                )
            );

            await writeRepository.AddAsync(
                CreateRetailerProduct(
                    data.FirstItemId,
                    data.SecondRetailerId,
                    "PRODUCT-2"
                )
            );

            await writeRepository.AddAsync(
                CreateRetailerProduct(
                    data.SecondItemId,
                    data.FirstRetailerId,
                    "PRODUCT-3"
                )
            );
        }

        await using var readContext =
            CreateDbContext();

        var readRepository =
            new RetailerProductRepository(
                readContext
            );

        // Act
        var result =
            await readRepository
                .GetByItemIdAsync(
                    data.FirstItemId
                );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.All(
            result,
            product =>
                Assert.Equal(
                    data.FirstItemId,
                    product.ItemId
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenRetailerAndExternalIdAreDuplicated_DatabaseRejectsDuplicate()
    {
        // Arrange
        var seeded =
            await SeedRetailerAndItemAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new RetailerProductRepository(
                context
            );

        await repository.AddAsync(
            CreateRetailerProduct(
                seeded.ItemId,
                seeded.RetailerId,
                "PRODUCT-123"
            )
        );

        var duplicate =
            CreateRetailerProduct(
                seeded.ItemId,
                seeded.RetailerId,
                "PRODUCT-123"
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
        var data =
            await SeedTwoRetailersAndOneItemAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new RetailerProductRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                CreateRetailerProduct(
                    data.ItemId,
                    data.FirstRetailerId,
                    "PRODUCT-123"
                )
            );

            await writeRepository.AddAsync(
                CreateRetailerProduct(
                    data.ItemId,
                    data.SecondRetailerId,
                    "PRODUCT-123"
                )
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var rows =
            await readContext
                .Set<RetailerProduct>()
                .Where(
                    product =>
                        product.ExternalProductId ==
                        "PRODUCT-123"
                )
                .ToListAsync();

        Assert.Equal(
            2,
            rows.Count
        );

        Assert.Contains(
            rows,
            product =>
                product.RetailerId ==
                data.FirstRetailerId
        );

        Assert.Contains(
            rows,
            product =>
                product.RetailerId ==
                data.SecondRetailerId
        );
    }

    [Fact]
    public async Task UpdateAsync_PersistsProductChanges()
    {
        // Arrange
        var productId =
            await SeedRetailerProductAsync();

        await using (
            var updateContext =
                CreateDbContext())
        {
            var updateRepository =
                new RetailerProductRepository(
                    updateContext
                );

            var product =
                await updateRepository.GetByIdAsync(
                    productId
                );

            Assert.NotNull(product);

            product.Name =
                "Updated Product";

            product.Brand =
                "Updated Brand";

            product.Size =
                "24 oz";

            product.Upc =
                "999999999999";

            product.MatchMethod =
                ProductMatchMethod.Deterministic;

            product.MatchConfidence =
                0.95m;

            product.IsActive =
                false;

            product.LastSeenAt =
                DateTime.UtcNow;

            // Act
            await updateRepository.UpdateAsync(
                product
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var stored =
            await readContext
                .Set<RetailerProduct>()
                .SingleAsync(
                    product =>
                        product.Id ==
                        productId
                );

        Assert.Equal(
            "Updated Product",
            stored.Name
        );

        Assert.Equal(
            "Updated Brand",
            stored.Brand
        );

        Assert.Equal(
            "24 oz",
            stored.Size
        );

        Assert.Equal(
            "999999999999",
            stored.Upc
        );

        Assert.Equal(
            ProductMatchMethod.Deterministic,
            stored.MatchMethod
        );

        Assert.Equal(
            0.95m,
            stored.MatchConfidence
        );

        Assert.False(
            stored.IsActive
        );
    }

    [Fact]
    public async Task AddAsync_WhenItemDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        await using var context =
            CreateDbContext();

        var repository =
            new RetailerProductRepository(
                context
            );

        var product =
            CreateRetailerProduct(
                itemId: 999999,
                retailerId: retailerId,
                externalProductId:
                    "PRODUCT-123"
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    product
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenRetailerDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var itemId =
            await SeedItemAsync(
                "Whole Milk"
            );

        await using var context =
            CreateDbContext();

        var repository =
            new RetailerProductRepository(
                context
            );

        var product =
            CreateRetailerProduct(
                itemId,
                retailerId: 999999,
                externalProductId:
                    "PRODUCT-123"
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    product
                )
        );
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    private async Task<(
        int RetailerId,
        int ItemId)>
        SeedRetailerAndItemAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Retailer>()
            .Add(retailer);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return (
            retailer.Id,
            item.Id
        );
    }

    private async Task<int>
        SeedRetailerProductAsync()
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                "Kroger"
            );

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
                "PRODUCT-123"
            );

        context.Set<RetailerProduct>()
            .Add(product);

        await context.SaveChangesAsync();

        return product.Id;
    }

    private async Task<int>
        SeedRetailerAsync(
            string name)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                name
            );

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        return retailer.Id;
    }

    private async Task<int>
        SeedItemAsync(
            string name)
    {
        await using var context =
            CreateDbContext();

        var item =
            CreateItem(
                name
            );

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return item.Id;
    }

    private async Task<(
        int FirstRetailerId,
        int SecondRetailerId,
        int ItemId)>
        SeedTwoRetailersAndOneItemAsync()
    {
        await using var context =
            CreateDbContext();

        var firstRetailer =
            CreateRetailer(
                "Kroger"
            );

        var secondRetailer =
            CreateRetailer(
                "Harris Teeter"
            );

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Retailer>()
            .AddRange(
                firstRetailer,
                secondRetailer
            );

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return (
            firstRetailer.Id,
            secondRetailer.Id,
            item.Id
        );
    }

    private async Task<(
        int FirstRetailerId,
        int SecondRetailerId,
        int FirstItemId,
        int SecondItemId)>
        SeedTwoRetailersAndTwoItemsAsync()
    {
        await using var context =
            CreateDbContext();

        var firstRetailer =
            CreateRetailer(
                "Kroger"
            );

        var secondRetailer =
            CreateRetailer(
                "Harris Teeter"
            );

        var firstItem =
            CreateItem(
                "Whole Milk"
            );

        var secondItem =
            CreateItem(
                "Large Eggs"
            );

        context.Set<Retailer>()
            .AddRange(
                firstRetailer,
                secondRetailer
            );

        context.Set<Item>()
            .AddRange(
                firstItem,
                secondItem
            );

        await context.SaveChangesAsync();

        return (
            firstRetailer.Id,
            secondRetailer.Id,
            firstItem.Id,
            secondItem.Id
        );
    }

    private static Retailer
        CreateRetailer(
            string name)
    {
        return new Retailer
        {
            Name = name,

            SupportsMembership =
                true,

            IsActive =
                true,

            CreatedAt =
                DateTime.UtcNow
        };
    }

    private static Item
        CreateItem(
            string name)
    {
        var now =
            DateTime.UtcNow;

        return new Item
        {
            Name =
                name,

            IsActive =
                true,

            CreatedAt =
                now,

            UpdatedAt =
                now
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

            Brand =
                "Test Brand",

            Size =
                "12 oz",

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