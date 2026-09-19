using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Repositories;

public class ItemRepositoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_PersistsItem()
    {
        // Arrange
        var now =
            DateTime.UtcNow;

        var item =
            new Item
            {
                Name = "Whole Milk",
                Brand = "Kroger",
                Size = "1 gallon",
                Category = "Dairy",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new ItemRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                item
            );
        }

        // Assert using a fresh DbContext.
        await using var readContext =
            CreateDbContext();

        var stored =
            await readContext
                .Set<Item>()
                .SingleAsync(
                    row =>
                        row.Id ==
                        item.Id
                );

        Assert.True(
            stored.Id > 0
        );

        Assert.Equal(
            "Whole Milk",
            stored.Name
        );

        Assert.Equal(
            "Kroger",
            stored.Brand
        );

        Assert.Equal(
            "1 gallon",
            stored.Size
        );

        Assert.Equal(
            "Dairy",
            stored.Category
        );

        Assert.True(
            stored.IsActive
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenItemExists_ReturnsItem()
    {
        // Arrange
        var itemId =
            await SeedItemAsync(
                name: "Whole Milk",
                isActive: true
            );

        await using var context =
            CreateDbContext();

        var repository =
            new ItemRepository(
                context
            );

        // Act
        var result =
            await repository.GetByIdAsync(
                itemId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            itemId,
            result.Id
        );

        Assert.Equal(
            "Whole Milk",
            result.Name
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenItemDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context =
            CreateDbContext();

        var repository =
            new ItemRepository(
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
    public async Task GetActiveAsync_ReturnsOnlyActiveItems()
    {
        // Arrange
        var firstActiveItemId =
            await SeedItemAsync(
                name: "Whole Milk",
                isActive: true
            );

        var secondActiveItemId =
            await SeedItemAsync(
                name: "Large Eggs",
                isActive: true
            );

        var inactiveItemId =
            await SeedItemAsync(
                name: "Discontinued Item",
                isActive: false
            );

        await using var context =
            CreateDbContext();

        var repository =
            new ItemRepository(
                context
            );

        // Act
        var result =
            await repository.GetActiveAsync();

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.Contains(
            result,
            item =>
                item.Id ==
                firstActiveItemId
        );

        Assert.Contains(
            result,
            item =>
                item.Id ==
                secondActiveItemId
        );

        Assert.DoesNotContain(
            result,
            item =>
                item.Id ==
                inactiveItemId
        );

        Assert.All(
            result,
            item =>
                Assert.True(
                    item.IsActive
                )
        );
    }

    private async Task<int> SeedItemAsync(
        string name,
        bool isActive)
    {
        await using var context =
            CreateDbContext();

        var now =
            DateTime.UtcNow;

        var item =
            new Item
            {
                Name = name,

                IsActive =
                    isActive,

                CreatedAt =
                    now,

                UpdatedAt =
                    now
            };

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return item.Id;
    }
}