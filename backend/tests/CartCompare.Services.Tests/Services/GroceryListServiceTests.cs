using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class GroceryListServiceTests
{
    private readonly Mock<IGroceryListRepository>
        _groceryListRepositoryMock;

    private readonly Mock<IItemRepository>
        _itemRepositoryMock;

    private readonly GroceryListService
        _service;

    public GroceryListServiceTests()
    {
        _groceryListRepositoryMock =
            new Mock<IGroceryListRepository>();

        _itemRepositoryMock =
            new Mock<IItemRepository>();

        _service =
            new GroceryListService(
                _groceryListRepositoryMock.Object,
                _itemRepositoryMock.Object
            );
    }

    [Fact]
    public async Task GetItemsAsync_WhenUserHasItems_ReturnsMappedDetails()
    {
        // Arrange
        var userId = 20;

        var milk =
            CreateItem(
                id: 1,
                name: "Whole Milk",
                brand: "Kroger",
                size: "1 gallon",
                category: "Dairy"
            );

        var eggs =
            CreateItem(
                id: 2,
                name: "Large Eggs",
                size: "12 count",
                category: "Dairy"
            );

        var milkListItem =
            CreateGroceryListItem(
                id: 100,
                userId: userId,
                itemId: milk.Id,
                quantity: 2
            );

        var eggsListItem =
            CreateGroceryListItem(
                id: 101,
                userId: userId,
                itemId: eggs.Id,
                quantity: 1
            );

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.GetByUserIdAsync(
                    userId
                )
            )
            .ReturnsAsync(
                new List<GroceryListItem>
                {
                    milkListItem,
                    eggsListItem
                }
            );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    milk.Id
                )
            )
            .ReturnsAsync(milk);

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    eggs.Id
                )
            )
            .ReturnsAsync(eggs);

        // Act
        var result =
            await _service.GetItemsAsync(
                userId
            );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        var milkResult =
            result.Single(
                entry =>
                    entry.ItemId ==
                    milk.Id
            );

        Assert.Equal(
            milkListItem.Id,
            milkResult.GroceryListItemId
        );

        Assert.Equal(
            "Whole Milk",
            milkResult.ItemName
        );

        Assert.Equal(
            "Kroger",
            milkResult.Brand
        );

        Assert.Equal(
            "1 gallon",
            milkResult.Size
        );

        Assert.Equal(
            "Dairy",
            milkResult.Category
        );

        Assert.Equal(
            2,
            milkResult.Quantity
        );

        Assert.Equal(
            milkListItem.CreatedAt,
            milkResult.CreatedAt
        );

        Assert.Equal(
            milkListItem.UpdatedAt,
            milkResult.UpdatedAt
        );
    }

    [Fact]
    public async Task GetItemsAsync_WhenUserHasNoItems_ReturnsEmptyList()
    {
        // Arrange
        _groceryListRepositoryMock
            .Setup(repository =>
                repository.GetByUserIdAsync(
                    20
                )
            )
            .ReturnsAsync(
                new List<GroceryListItem>()
            );

        // Act
        var result =
            await _service.GetItemsAsync(
                20
            );

        // Assert
        Assert.Empty(result);

        _itemRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SetQuantityAsync_WhenItemDoesNotExist_ReturnsNull()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    999
                )
            )
            .ReturnsAsync(
                (Item?)null
            );

        // Act
        var result =
            await _service.SetQuantityAsync(
                userId: 20,
                itemId: 999,
                quantity: 2
            );

        // Assert
        Assert.Null(result);

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.GetByUserAndItemAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>()
                ),
            Times.Never
        );

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<GroceryListItem>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SetQuantityAsync_WhenListItemDoesNotExist_CreatesIt()
    {
        // Arrange
        var userId = 20;

        var item =
            CreateItem(
                id: 1,
                name: "Whole Milk",
                size: "1 gallon"
            );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndItemAsync(
                    userId,
                    item.Id
                )
            )
            .ReturnsAsync(
                (GroceryListItem?)null
            );

        GroceryListItem? addedItem =
            null;

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<GroceryListItem>()
                )
            )
            .Callback<GroceryListItem>(
                groceryListItem =>
                {
                    groceryListItem.Id = 100;

                    addedItem =
                        groceryListItem;
                }
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.SetQuantityAsync(
                userId,
                item.Id,
                3
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(addedItem);

        Assert.Equal(
            userId,
            addedItem.UserId
        );

        Assert.Equal(
            item.Id,
            addedItem.ItemId
        );

        Assert.Equal(
            3,
            addedItem.Quantity
        );

        Assert.InRange(
            addedItem.CreatedAt,
            before,
            after
        );

        Assert.InRange(
            addedItem.UpdatedAt,
            before,
            after
        );

        Assert.Equal(
            100,
            result.GroceryListItemId
        );

        Assert.Equal(
            item.Id,
            result.ItemId
        );

        Assert.Equal(
            item.Name,
            result.ItemName
        );

        Assert.Equal(
            3,
            result.Quantity
        );

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<GroceryListItem>()
                ),
            Times.Once
        );

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    It.IsAny<GroceryListItem>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SetQuantityAsync_WhenListItemAlreadyExists_UpdatesQuantity()
    {
        // Arrange
        var userId = 20;

        var item =
            CreateItem(
                id: 1,
                name: "Whole Milk"
            );

        var existing =
            CreateGroceryListItem(
                id: 100,
                userId: userId,
                itemId: item.Id,
                quantity: 1
            );

        var originalCreatedAt =
            existing.CreatedAt;

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndItemAsync(
                    userId,
                    item.Id
                )
            )
            .ReturnsAsync(existing);

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.UpdateAsync(
                    existing
                )
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.SetQuantityAsync(
                userId,
                item.Id,
                5
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            5,
            existing.Quantity
        );

        Assert.Equal(
            originalCreatedAt,
            existing.CreatedAt
        );

        Assert.InRange(
            existing.UpdatedAt,
            before,
            after
        );

        Assert.Equal(
            5,
            result.Quantity
        );

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.UpdateAsync(
                    existing
                ),
            Times.Once
        );

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<GroceryListItem>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RemoveItemAsync_WhenItemIsNotOnList_ReturnsFalse()
    {
        // Arrange
        _groceryListRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndItemAsync(
                    20,
                    1
                )
            )
            .ReturnsAsync(
                (GroceryListItem?)null
            );

        // Act
        var result =
            await _service.RemoveItemAsync(
                20,
                1
            );

        // Assert
        Assert.False(result);

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.RemoveAsync(
                    It.IsAny<GroceryListItem>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RemoveItemAsync_WhenItemExists_RemovesItAndReturnsTrue()
    {
        // Arrange
        var existing =
            CreateGroceryListItem(
                id: 100,
                userId: 20,
                itemId: 1,
                quantity: 2
            );

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndItemAsync(
                    existing.UserId,
                    existing.ItemId
                )
            )
            .ReturnsAsync(existing);

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.RemoveAsync(
                    existing
                )
            )
            .Returns(Task.CompletedTask);

        // Act
        var result =
            await _service.RemoveItemAsync(
                existing.UserId,
                existing.ItemId
            );

        // Assert
        Assert.True(result);

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.RemoveAsync(
                    existing
                ),
            Times.Once
        );
    }

    private static Item CreateItem(
        int id,
        string name,
        string? brand = null,
        string? size = null,
        string? category = null)
    {
        return new Item
        {
            Id = id,
            Name = name,
            Brand = brand,
            Size = size,
            Category = category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static GroceryListItem
        CreateGroceryListItem(
            int id,
            int userId,
            int itemId,
            int quantity)
    {
        return new GroceryListItem
        {
            Id = id,
            UserId = userId,
            ItemId = itemId,
            Quantity = quantity,

            CreatedAt =
                DateTime.UtcNow.AddMinutes(-10),

            UpdatedAt =
                DateTime.UtcNow.AddMinutes(-5)
        };
    }
}