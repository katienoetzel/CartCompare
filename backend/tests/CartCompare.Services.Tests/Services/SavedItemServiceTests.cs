using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class SavedItemServiceTests
{
    private readonly Mock<ISavedItemRepository>
        _savedItemRepositoryMock;

    private readonly Mock<IItemRepository>
        _itemRepositoryMock;

    private readonly SavedItemService
        _service;

    public SavedItemServiceTests()
    {
        _savedItemRepositoryMock =
            new Mock<ISavedItemRepository>();

        _itemRepositoryMock =
            new Mock<IItemRepository>();

        _service =
            new SavedItemService(
                _savedItemRepositoryMock.Object,
                _itemRepositoryMock.Object
            );
    }

    [Fact]
    public async Task GetSavedItemsAsync_WhenUserHasSavedItems_ReturnsMappedDetails()
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

        var savedMilk =
            CreateSavedItem(
                id: 100,
                userId: userId,
                itemId: milk.Id
            );

        var savedEggs =
            CreateSavedItem(
                id: 101,
                userId: userId,
                itemId: eggs.Id
            );

        _savedItemRepositoryMock
            .Setup(repository =>
                repository.GetByUserIdAsync(
                    userId
                )
            )
            .ReturnsAsync(
                new List<SavedItem>
                {
                    savedMilk,
                    savedEggs
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
            await _service.GetSavedItemsAsync(
                userId
            );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        var milkResult =
            result.Single(
                saved =>
                    saved.ItemId ==
                    milk.Id
            );

        Assert.Equal(
            savedMilk.Id,
            milkResult.SavedItemId
        );

        Assert.Equal(
            milk.Id,
            milkResult.ItemId
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
            savedMilk.CreatedAt,
            milkResult.CreatedAt
        );
    }

    [Fact]
    public async Task GetSavedItemsAsync_WhenUserHasNoSavedItems_ReturnsEmptyList()
    {
        // Arrange
        _savedItemRepositoryMock
            .Setup(repository =>
                repository.GetByUserIdAsync(
                    20
                )
            )
            .ReturnsAsync(
                new List<SavedItem>()
            );

        // Act
        var result =
            await _service.GetSavedItemsAsync(
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
    public async Task SaveItemAsync_WhenItemDoesNotExist_ReturnsNull()
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
            await _service.SaveItemAsync(
                userId: 20,
                itemId: 999
            );

        // Assert
        Assert.Null(result);

        _savedItemRepositoryMock.Verify(
            repository =>
                repository.GetByUserAndItemAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>()
                ),
            Times.Never
        );

        _savedItemRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<SavedItem>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SaveItemAsync_WhenItemIsNotAlreadySaved_CreatesSavedItem()
    {
        // Arrange
        var userId = 20;

        var item =
            CreateItem(
                id: 1,
                name: "Whole Milk",
                brand: "Kroger",
                size: "1 gallon",
                category: "Dairy"
            );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _savedItemRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndItemAsync(
                    userId,
                    item.Id
                )
            )
            .ReturnsAsync(
                (SavedItem?)null
            );

        SavedItem? addedSavedItem =
            null;

        _savedItemRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<SavedItem>()
                )
            )
            .Callback<SavedItem>(
                savedItem =>
                {
                    savedItem.Id = 100;
                    addedSavedItem = savedItem;
                }
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.SaveItemAsync(
                userId,
                item.Id
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(addedSavedItem);

        Assert.Equal(
            userId,
            addedSavedItem.UserId
        );

        Assert.Equal(
            item.Id,
            addedSavedItem.ItemId
        );

        Assert.InRange(
            addedSavedItem.CreatedAt,
            before,
            after
        );

        Assert.Equal(
            100,
            result.SavedItemId
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
            item.Brand,
            result.Brand
        );

        Assert.Equal(
            item.Size,
            result.Size
        );

        Assert.Equal(
            item.Category,
            result.Category
        );

        Assert.Equal(
            addedSavedItem.CreatedAt,
            result.CreatedAt
        );

        _savedItemRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<SavedItem>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task SaveItemAsync_WhenItemIsAlreadySaved_ReturnsExistingDetailsWithoutAddingDuplicate()
    {
        // Arrange
        var userId = 20;

        var item =
            CreateItem(
                id: 1,
                name: "Whole Milk",
                brand: "Kroger",
                size: "1 gallon"
            );

        var existingSavedItem =
            CreateSavedItem(
                id: 100,
                userId: userId,
                itemId: item.Id
            );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _savedItemRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndItemAsync(
                    userId,
                    item.Id
                )
            )
            .ReturnsAsync(
                existingSavedItem
            );

        // Act
        var result =
            await _service.SaveItemAsync(
                userId,
                item.Id
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            existingSavedItem.Id,
            result.SavedItemId
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
            existingSavedItem.CreatedAt,
            result.CreatedAt
        );

        _savedItemRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<SavedItem>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RemoveSavedItemAsync_WhenSavedItemDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _savedItemRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndItemAsync(
                    20,
                    1
                )
            )
            .ReturnsAsync(
                (SavedItem?)null
            );

        // Act
        var result =
            await _service.RemoveSavedItemAsync(
                20,
                1
            );

        // Assert
        Assert.False(result);

        _savedItemRepositoryMock.Verify(
            repository =>
                repository.RemoveAsync(
                    It.IsAny<SavedItem>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RemoveSavedItemAsync_WhenSavedItemExists_RemovesItAndReturnsTrue()
    {
        // Arrange
        var existingSavedItem =
            CreateSavedItem(
                id: 100,
                userId: 20,
                itemId: 1
            );

        _savedItemRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndItemAsync(
                    existingSavedItem.UserId,
                    existingSavedItem.ItemId
                )
            )
            .ReturnsAsync(
                existingSavedItem
            );

        _savedItemRepositoryMock
            .Setup(repository =>
                repository.RemoveAsync(
                    existingSavedItem
                )
            )
            .Returns(Task.CompletedTask);

        // Act
        var result =
            await _service.RemoveSavedItemAsync(
                existingSavedItem.UserId,
                existingSavedItem.ItemId
            );

        // Assert
        Assert.True(result);

        _savedItemRepositoryMock.Verify(
            repository =>
                repository.RemoveAsync(
                    existingSavedItem
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

    private static SavedItem CreateSavedItem(
        int id,
        int userId,
        int itemId)
    {
        return new SavedItem
        {
            Id = id,
            UserId = userId,
            ItemId = itemId,

            CreatedAt =
                DateTime.UtcNow.AddMinutes(-10)
        };
    }
}