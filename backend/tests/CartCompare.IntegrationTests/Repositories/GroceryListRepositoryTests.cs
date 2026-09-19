using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Repositories;

public class GroceryListRepositoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_PersistsGroceryListItem()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        var groceryListItem =
            new GroceryListItem
            {
                UserId = seeded.UserId,
                ItemId = seeded.ItemId,
                Quantity = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new GroceryListRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                groceryListItem
            );
        }

        // Assert using a NEW DbContext.
        await using var readContext =
            CreateDbContext();

        var readRepository =
            new GroceryListRepository(
                readContext
            );

        var stored =
            await readRepository
                .GetByUserAndItemAsync(
                    seeded.UserId,
                    seeded.ItemId
                );

        Assert.NotNull(stored);

        Assert.True(
            stored.Id > 0
        );

        Assert.Equal(
            seeded.UserId,
            stored.UserId
        );

        Assert.Equal(
            seeded.ItemId,
            stored.ItemId
        );

        Assert.Equal(
            2,
            stored.Quantity
        );
    }

    [Fact]
    public async Task GetByUserAndItemAsync_WhenRowDoesNotExist_ReturnsNull()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new GroceryListRepository(
                context
            );

        // Act
        var result =
            await repository
                .GetByUserAndItemAsync(
                    seeded.UserId,
                    seeded.ItemId
                );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyRequestedUsersRows()
    {
        // Arrange
        var data =
            await SeedTwoUsersAndTwoItemsAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new GroceryListRepository(
                    writeContext
                );

            await writeRepository.AddAsync(
                CreateGroceryListItem(
                    data.FirstUserId,
                    data.FirstItemId,
                    quantity: 2
                )
            );

            await writeRepository.AddAsync(
                CreateGroceryListItem(
                    data.FirstUserId,
                    data.SecondItemId,
                    quantity: 1
                )
            );

            await writeRepository.AddAsync(
                CreateGroceryListItem(
                    data.SecondUserId,
                    data.FirstItemId,
                    quantity: 5
                )
            );
        }

        await using var readContext =
            CreateDbContext();

        var readRepository =
            new GroceryListRepository(
                readContext
            );

        // Act
        var result =
            await readRepository.GetByUserIdAsync(
                data.FirstUserId
            );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.All(
            result,
            row =>
                Assert.Equal(
                    data.FirstUserId,
                    row.UserId
                )
        );

        Assert.Contains(
            result,
            row =>
                row.ItemId ==
                data.FirstItemId
        );

        Assert.Contains(
            result,
            row =>
                row.ItemId ==
                data.SecondItemId
        );
    }

    [Fact]
    public async Task AddAsync_WhenSameUserAndItemAreAddedTwice_DatabaseRejectsDuplicate()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new GroceryListRepository(
                context
            );

        await repository.AddAsync(
            CreateGroceryListItem(
                seeded.UserId,
                seeded.ItemId,
                1
            )
        );

        var duplicate =
            CreateGroceryListItem(
                seeded.UserId,
                seeded.ItemId,
                3
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
    public async Task AddAsync_WhenQuantityIsZero_DatabaseRejectsRow()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new GroceryListRepository(
                context
            );

        var row =
            CreateGroceryListItem(
                seeded.UserId,
                seeded.ItemId,
                quantity: 0
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    row
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenQuantityIsNegative_DatabaseRejectsRow()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new GroceryListRepository(
                context
            );

        var row =
            CreateGroceryListItem(
                seeded.UserId,
                seeded.ItemId,
                quantity: -2
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    row
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenDifferentUsersUseSameItem_AllowsBothRows()
    {
        // Arrange
        var data =
            await SeedTwoUsersAndOneItemAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new GroceryListRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                CreateGroceryListItem(
                    data.FirstUserId,
                    data.ItemId,
                    1
                )
            );

            await writeRepository.AddAsync(
                CreateGroceryListItem(
                    data.SecondUserId,
                    data.ItemId,
                    4
                )
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var rows =
            await readContext
                .Set<GroceryListItem>()
                .Where(
                    row =>
                        row.ItemId ==
                        data.ItemId
                )
                .ToListAsync();

        Assert.Equal(
            2,
            rows.Count
        );

        Assert.Contains(
            rows,
            row =>
                row.UserId ==
                data.FirstUserId
        );

        Assert.Contains(
            rows,
            row =>
                row.UserId ==
                data.SecondUserId
        );
    }

    [Fact]
    public async Task UpdateAsync_PersistsNewQuantity()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        int groceryListItemId;

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new GroceryListRepository(
                    writeContext
                );

            var row =
                CreateGroceryListItem(
                    seeded.UserId,
                    seeded.ItemId,
                    1
                );

            await writeRepository.AddAsync(
                row
            );

            groceryListItemId =
                row.Id;
        }

        await using (
            var updateContext =
                CreateDbContext())
        {
            var updateRepository =
                new GroceryListRepository(
                    updateContext
                );

            var row =
                await updateRepository
                    .GetByUserAndItemAsync(
                        seeded.UserId,
                        seeded.ItemId
                    );

            Assert.NotNull(row);

            row.Quantity = 7;
            row.UpdatedAt = DateTime.UtcNow;

            // Act
            await updateRepository.UpdateAsync(
                row
            );
        }

        // Assert with another fresh context.
        await using var readContext =
            CreateDbContext();

        var stored =
            await readContext
                .Set<GroceryListItem>()
                .SingleAsync(
                    row =>
                        row.Id ==
                        groceryListItemId
                );

        Assert.Equal(
            7,
            stored.Quantity
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenQuantityBecomesZero_DatabaseRejectsUpdate()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new GroceryListRepository(
                context
            );

        var row =
            CreateGroceryListItem(
                seeded.UserId,
                seeded.ItemId,
                2
            );

        await repository.AddAsync(
            row
        );

        row.Quantity = 0;
        row.UpdatedAt = DateTime.UtcNow;

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.UpdateAsync(
                    row
                )
        );
    }

    [Fact]
    public async Task RemoveAsync_RemovesRowFromDatabase()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        int groceryListItemId;

        await using (
            var context =
                CreateDbContext())
        {
            var repository =
                new GroceryListRepository(
                    context
                );

            var row =
                CreateGroceryListItem(
                    seeded.UserId,
                    seeded.ItemId,
                    2
                );

            await repository.AddAsync(
                row
            );

            groceryListItemId =
                row.Id;

            // Act
            await repository.RemoveAsync(
                row
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var exists =
            await readContext
                .Set<GroceryListItem>()
                .AnyAsync(
                    row =>
                        row.Id ==
                        groceryListItemId
                );

        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsync_WhenUserDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var itemId =
            await SeedItemAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new GroceryListRepository(
                context
            );

        var row =
            CreateGroceryListItem(
                userId: 999999,
                itemId: itemId,
                quantity: 1
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    row
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenItemDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var userId =
            await SeedUserAsync(
                "missing-item-user@example.com"
            );

        await using var context =
            CreateDbContext();

        var repository =
            new GroceryListRepository(
                context
            );

        var row =
            CreateGroceryListItem(
                userId,
                itemId: 999999,
                quantity: 1
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    row
                )
        );
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    private static GroceryListItem
        CreateGroceryListItem(
            int userId,
            int itemId,
            int quantity)
    {
        var now =
            DateTime.UtcNow;

        return new GroceryListItem
        {
            UserId = userId,
            ItemId = itemId,
            Quantity = quantity,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private async Task<(int UserId, int ItemId)>
        SeedUserAndItemAsync()
    {
        await using var context =
            CreateDbContext();

        var user =
            CreateUser(
                "grocery-user@example.com"
            );

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<ApplicationUser>()
            .Add(user);

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return (
            user.Id,
            item.Id
        );
    }

    private async Task<int> SeedUserAsync(
        string email)
    {
        await using var context =
            CreateDbContext();

        var user =
            CreateUser(
                email
            );

        context.Set<ApplicationUser>()
            .Add(user);

        await context.SaveChangesAsync();

        return user.Id;
    }

    private async Task<int> SeedItemAsync()
    {
        await using var context =
            CreateDbContext();

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return item.Id;
    }

    private async Task<(
        int FirstUserId,
        int SecondUserId,
        int ItemId)>
        SeedTwoUsersAndOneItemAsync()
    {
        await using var context =
            CreateDbContext();

        var firstUser =
            CreateUser(
                "first-grocery-user@example.com"
            );

        var secondUser =
            CreateUser(
                "second-grocery-user@example.com"
            );

        var item =
            CreateItem(
                "Whole Milk"
            );

        context.Set<ApplicationUser>()
            .AddRange(
                firstUser,
                secondUser
            );

        context.Set<Item>()
            .Add(item);

        await context.SaveChangesAsync();

        return (
            firstUser.Id,
            secondUser.Id,
            item.Id
        );
    }

    private async Task<(
        int FirstUserId,
        int SecondUserId,
        int FirstItemId,
        int SecondItemId)>
        SeedTwoUsersAndTwoItemsAsync()
    {
        await using var context =
            CreateDbContext();

        var firstUser =
            CreateUser(
                "first-grocery-user@example.com"
            );

        var secondUser =
            CreateUser(
                "second-grocery-user@example.com"
            );

        var firstItem =
            CreateItem(
                "Whole Milk"
            );

        var secondItem =
            CreateItem(
                "Large Eggs"
            );

        context.Set<ApplicationUser>()
            .AddRange(
                firstUser,
                secondUser
            );

        context.Set<Item>()
            .AddRange(
                firstItem,
                secondItem
            );

        await context.SaveChangesAsync();

        return (
            firstUser.Id,
            secondUser.Id,
            firstItem.Id,
            secondItem.Id
        );
    }

    private static ApplicationUser CreateUser(
        string email)
    {
        return new ApplicationUser
        {
            UserName = email,

            NormalizedUserName =
                email.ToUpperInvariant(),

            Email = email,

            NormalizedEmail =
                email.ToUpperInvariant(),

            EmailConfirmed = true
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
}