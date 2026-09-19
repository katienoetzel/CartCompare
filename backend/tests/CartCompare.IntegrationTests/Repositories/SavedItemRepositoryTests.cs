using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Repositories;

public class SavedItemRepositoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_PersistsSavedItem()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        var savedItem =
            new SavedItem
            {
                UserId = seeded.UserId,
                ItemId = seeded.ItemId,
                CreatedAt = DateTime.UtcNow
            };

        await using (
            var writeContext =
                CreateDbContext())
        {
            var repository =
                new SavedItemRepository(
                    writeContext
                );

            // Act
            await repository.AddAsync(
                savedItem
            );
        }

        // Assert
        // Use a NEW DbContext so this proves
        // the row was actually written to PostgreSQL,
        // not merely tracked in memory.
        await using var readContext =
            CreateDbContext();

        var readRepository =
            new SavedItemRepository(
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
    }

    [Fact]
    public async Task GetByUserAndItemAsync_WhenSavedItemDoesNotExist_ReturnsNull()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new SavedItemRepository(
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
    public async Task GetByUserIdAsync_ReturnsOnlyRequestedUsersSavedItems()
    {
        // Arrange
        var data =
            await SeedTwoUsersAndTwoItemsAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var repository =
                new SavedItemRepository(
                    writeContext
                );

            await repository.AddAsync(
                new SavedItem
                {
                    UserId = data.FirstUserId,
                    ItemId = data.FirstItemId,
                    CreatedAt = DateTime.UtcNow
                }
            );

            await repository.AddAsync(
                new SavedItem
                {
                    UserId = data.FirstUserId,
                    ItemId = data.SecondItemId,
                    CreatedAt = DateTime.UtcNow
                }
            );

            await repository.AddAsync(
                new SavedItem
                {
                    UserId = data.SecondUserId,
                    ItemId = data.FirstItemId,
                    CreatedAt = DateTime.UtcNow
                }
            );
        }

        await using var readContext =
            CreateDbContext();

        var readRepository =
            new SavedItemRepository(
                readContext
            );

        // Act
        var result =
            await readRepository
                .GetByUserIdAsync(
                    data.FirstUserId
                );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.All(
            result,
            savedItem =>
                Assert.Equal(
                    data.FirstUserId,
                    savedItem.UserId
                )
        );

        Assert.Contains(
            result,
            savedItem =>
                savedItem.ItemId ==
                data.FirstItemId
        );

        Assert.Contains(
            result,
            savedItem =>
                savedItem.ItemId ==
                data.SecondItemId
        );

        Assert.DoesNotContain(
            result,
            savedItem =>
                savedItem.UserId ==
                data.SecondUserId
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
            new SavedItemRepository(
                context
            );

        await repository.AddAsync(
            new SavedItem
            {
                UserId = seeded.UserId,
                ItemId = seeded.ItemId,
                CreatedAt = DateTime.UtcNow
            }
        );

        var duplicate =
            new SavedItem
            {
                UserId = seeded.UserId,
                ItemId = seeded.ItemId,
                CreatedAt = DateTime.UtcNow
            };

        // Act + Assert
        await Assert.ThrowsAsync
            <DbUpdateException>(
                async () =>
                    await repository.AddAsync(
                        duplicate
                    )
            );
    }

    [Fact]
    public async Task AddAsync_WhenDifferentUsersSaveSameItem_AllowsBothRows()
    {
        // Arrange
        var data =
            await SeedTwoUsersAndOneItemAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var repository =
                new SavedItemRepository(
                    writeContext
                );

            // Act
            await repository.AddAsync(
                new SavedItem
                {
                    UserId = data.FirstUserId,
                    ItemId = data.ItemId,
                    CreatedAt = DateTime.UtcNow
                }
            );

            await repository.AddAsync(
                new SavedItem
                {
                    UserId = data.SecondUserId,
                    ItemId = data.ItemId,
                    CreatedAt = DateTime.UtcNow
                }
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var firstUserSavedItems =
            await readContext
                .Set<SavedItem>()
                .Where(
                    savedItem =>
                        savedItem.UserId ==
                        data.FirstUserId
                )
                .ToListAsync();

        var secondUserSavedItems =
            await readContext
                .Set<SavedItem>()
                .Where(
                    savedItem =>
                        savedItem.UserId ==
                        data.SecondUserId
                )
                .ToListAsync();

        Assert.Single(
            firstUserSavedItems
        );

        Assert.Single(
            secondUserSavedItems
        );

        Assert.Equal(
            data.ItemId,
            firstUserSavedItems[0].ItemId
        );

        Assert.Equal(
            data.ItemId,
            secondUserSavedItems[0].ItemId
        );
    }

    [Fact]
    public async Task RemoveAsync_RemovesSavedItemFromDatabase()
    {
        // Arrange
        var seeded =
            await SeedUserAndItemAsync();

        int savedItemId;

        await using (
            var writeContext =
                CreateDbContext())
        {
            var repository =
                new SavedItemRepository(
                    writeContext
                );

            var savedItem =
                new SavedItem
                {
                    UserId = seeded.UserId,
                    ItemId = seeded.ItemId,
                    CreatedAt = DateTime.UtcNow
                };

            await repository.AddAsync(
                savedItem
            );

            savedItemId =
                savedItem.Id;

            // Act
            await repository.RemoveAsync(
                savedItem
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var stillExists =
            await readContext
                .Set<SavedItem>()
                .AnyAsync(
                    savedItem =>
                        savedItem.Id ==
                        savedItemId
                );

        Assert.False(
            stillExists
        );
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
            new SavedItemRepository(
                context
            );

        var savedItem =
            new SavedItem
            {
                UserId = 999999,
                ItemId = itemId,
                CreatedAt = DateTime.UtcNow
            };

        // Act + Assert
        await Assert.ThrowsAsync
            <DbUpdateException>(
                async () =>
                    await repository.AddAsync(
                        savedItem
                    )
            );
    }

    [Fact]
    public async Task AddAsync_WhenItemDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var userId =
            await SeedUserAsync(
                "foreign-key-user@example.com"
            );

        await using var context =
            CreateDbContext();

        var repository =
            new SavedItemRepository(
                context
            );

        var savedItem =
            new SavedItem
            {
                UserId = userId,
                ItemId = 999999,
                CreatedAt = DateTime.UtcNow
            };

        // Act + Assert
        await Assert.ThrowsAsync
            <DbUpdateException>(
                async () =>
                    await repository.AddAsync(
                        savedItem
                    )
            );
    }

    // -------------------------------------------------
    // Seed helpers
    // -------------------------------------------------

    private async Task<(int UserId, int ItemId)>
        SeedUserAndItemAsync()
    {
        await using var context =
            CreateDbContext();

        var user =
            CreateUser(
                "saved-item-user@example.com"
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
                "first-user@example.com"
            );

        var secondUser =
            CreateUser(
                "second-user@example.com"
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
                "first-user@example.com"
            );

        var secondUser =
            CreateUser(
                "second-user@example.com"
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