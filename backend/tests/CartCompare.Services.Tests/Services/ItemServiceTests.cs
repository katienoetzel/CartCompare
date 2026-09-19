using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class ItemServiceTests
{
    private readonly Mock<IItemRepository>
        _itemRepositoryMock;

    private readonly ItemService
        _service;

    public ItemServiceTests()
    {
        _itemRepositoryMock =
            new Mock<IItemRepository>();

        _service =
            new ItemService(
                _itemRepositoryMock.Object
            );
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveItemsFromRepository()
    {
        // Arrange
        var items =
            new List<Item>
            {
                CreateItem(
                    id: 1,
                    name: "Whole Milk"
                ),

                CreateItem(
                    id: 2,
                    name: "Large Eggs"
                )
            };

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetActiveAsync()
            )
            .ReturnsAsync(items);

        // Act
        var result =
            await _service.GetActiveAsync();

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.Same(
            items,
            result
        );

        _itemRepositoryMock.Verify(
            repository =>
                repository.GetActiveAsync(),
            Times.Once
        );
    }

    [Fact]
    public async Task GetActiveAsync_WhenNoItemsExist_ReturnsEmptyList()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(repository =>
                repository.GetActiveAsync()
            )
            .ReturnsAsync(
                new List<Item>()
            );

        // Act
        var result =
            await _service.GetActiveAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenItemExists_ReturnsItem()
    {
        // Arrange
        var item =
            CreateItem(
                id: 1,
                name: "Whole Milk"
            );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        // Act
        var result =
            await _service.GetByIdAsync(
                item.Id
            );

        // Assert
        Assert.Same(
            item,
            result
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenItemDoesNotExist_ReturnsNull()
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
            await _service.GetByIdAsync(
                999
            );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_CreatesItemWithTrimmedMetadataAndTimestamps()
    {
        // Arrange
        Item? addedItem =
            null;

        _itemRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<Item>()
                )
            )
            .Callback<Item>(
                item =>
                {
                    item.Id = 100;
                    addedItem = item;
                }
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.CreateAsync(
                "  Whole Milk  ",
                "  Kroger  ",
                "  1 gallon  ",
                "  Dairy  "
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.NotNull(
            addedItem
        );

        Assert.Equal(
            "Whole Milk",
            addedItem.Name
        );

        Assert.Equal(
            "Kroger",
            addedItem.Brand
        );

        Assert.Equal(
            "1 gallon",
            addedItem.Size
        );

        Assert.Equal(
            "Dairy",
            addedItem.Category
        );

        Assert.True(
            addedItem.IsActive
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

        Assert.Same(
            addedItem,
            result
        );

        _itemRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<Item>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateAsync_WhenOptionalMetadataIsNull_PreservesNullValues()
    {
        // Arrange
        Item? addedItem =
            null;

        _itemRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<Item>()
                )
            )
            .Callback<Item>(
                item =>
                {
                    addedItem = item;
                }
            )
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateAsync(
            "Milk",
            null,
            null,
            null
        );

        // Assert
        Assert.NotNull(
            addedItem
        );

        Assert.Null(
            addedItem.Brand
        );

        Assert.Null(
            addedItem.Size
        );

        Assert.Null(
            addedItem.Category
        );
    }

    private static Item CreateItem(
        int id,
        string name)
    {
        return new Item
        {
            Id = id,
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}