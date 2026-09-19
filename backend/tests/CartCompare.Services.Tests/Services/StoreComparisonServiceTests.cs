using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class StoreComparisonServiceTests
{
    private readonly Mock<IGroceryListRepository>
        _groceryListRepositoryMock;

    private readonly Mock<IItemRepository>
        _itemRepositoryMock;

    private readonly Mock<IStoreLocationRepository>
        _storeLocationRepositoryMock;

    private readonly Mock<IRetailerProductRepository>
        _retailerProductRepositoryMock;

    private readonly Mock<IPriceService>
        _priceServiceMock;

    private readonly StoreComparisonService
        _service;

    public StoreComparisonServiceTests()
    {
        _groceryListRepositoryMock =
            new Mock<IGroceryListRepository>();

        _itemRepositoryMock =
            new Mock<IItemRepository>();

        _storeLocationRepositoryMock =
            new Mock<IStoreLocationRepository>();

        _retailerProductRepositoryMock =
            new Mock<IRetailerProductRepository>();

        _priceServiceMock =
            new Mock<IPriceService>();

        _service =
            new StoreComparisonService(
                _groceryListRepositoryMock.Object,
                _itemRepositoryMock.Object,
                _storeLocationRepositoryMock.Object,
                _retailerProductRepositoryMock.Object,
                _priceServiceMock.Object
            );
    }

    [Fact]
    public async Task CompareStoreAsync_WhenStoreDoesNotExist_ReturnsNull()
    {
        // Arrange
        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(10)
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

        // Act
        var result =
            await _service.CompareStoreAsync(
                userId: 20,
                storeLocationId: 10
            );

        // Assert
        Assert.Null(result);

        _groceryListRepositoryMock.Verify(
            repository =>
                repository.GetByUserIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CompareStoreAsync_WhenListItemHasUsablePrice_CalculatesQuantityAndSubtotal()
    {
        // Arrange
        var store =
            CreateStore(
                id: 10,
                retailerId: 1,
                name: "Kroger Downtown"
            );

        var item =
            CreateItem(
                id: 1,
                name: "Whole Milk"
            );

        var listItem =
            CreateGroceryListItem(
                itemId: item.Id,
                quantity: 3
            );

        var product =
            CreateRetailerProduct(
                id: 100,
                itemId: item.Id,
                retailerId: store.RetailerId,
                name: "Kroger Whole Milk"
            );

        SetupStoreAndList(
            userId: 20,
            store,
            new List<GroceryListItem>
            {
                listItem
            }
        );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(
                new List<RetailerProduct>
                {
                    product
                }
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    product.Id,
                    store.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    4.00m,
                    EffectivePriceType.Regular
                )
            );

        // Act
        var result =
            await _service.CompareStoreAsync(
                20,
                store.Id
            );

        // Assert
        Assert.NotNull(result);

        Assert.True(
            result.IsComplete
        );

        Assert.Equal(
            12.00m,
            result.KnownSubtotal
        );

        Assert.Equal(
            0,
            result.MissingItemCount
        );

        Assert.Single(
            result.Items
        );

        var comparedItem =
            result.Items[0];

        Assert.True(
            comparedItem.IsAvailable
        );

        Assert.Equal(
            item.Id,
            comparedItem.ItemId
        );

        Assert.Equal(
            3,
            comparedItem.Quantity
        );

        Assert.Equal(
            product.Id,
            comparedItem.RetailerProductId
        );

        Assert.Equal(
            4.00m,
            comparedItem.UnitPrice
        );

        Assert.Equal(
            12.00m,
            comparedItem.LineTotal
        );

        Assert.Equal(
            EffectivePriceType.Regular,
            comparedItem.PriceType
        );
    }

    [Fact]
    public async Task CompareStoreAsync_WhenMultipleProductsExist_UsesCheapestUsablePrice()
    {
        // Arrange
        var store =
            CreateStore();

        var item =
            CreateItem();

        var listItem =
            CreateGroceryListItem(
                itemId: item.Id,
                quantity: 1
            );

        var expensiveProduct =
            CreateRetailerProduct(
                id: 100,
                itemId: item.Id,
                retailerId: store.RetailerId,
                name: "Expensive Milk"
            );

        var cheapProduct =
            CreateRetailerProduct(
                id: 101,
                itemId: item.Id,
                retailerId: store.RetailerId,
                name: "Cheap Milk"
            );

        SetupStoreAndList(
            20,
            store,
            new List<GroceryListItem>
            {
                listItem
            }
        );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(
                new List<RetailerProduct>
                {
                    expensiveProduct,
                    cheapProduct
                }
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    expensiveProduct.Id,
                    store.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    5.00m,
                    EffectivePriceType.Regular
                )
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    cheapProduct.Id,
                    store.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    3.50m,
                    EffectivePriceType.Sale
                )
            );

        // Act
        var result =
            await _service.CompareStoreAsync(
                20,
                store.Id
            );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsComplete);

        Assert.Equal(
            3.50m,
            result.KnownSubtotal
        );

        Assert.Equal(
            cheapProduct.Id,
            result.Items[0]
                .RetailerProductId
        );

        Assert.Equal(
            3.50m,
            result.Items[0]
                .UnitPrice
        );

        Assert.Equal(
            EffectivePriceType.Sale,
            result.Items[0]
                .PriceType
        );
    }

    [Fact]
    public async Task CompareStoreAsync_IgnoresInactiveAndWrongRetailerProducts()
    {
        // Arrange
        var store =
            CreateStore(
                retailerId: 1
            );

        var item =
            CreateItem();

        var listItem =
            CreateGroceryListItem(
                itemId: item.Id
            );

        var inactiveProduct =
            CreateRetailerProduct(
                id: 100,
                itemId: item.Id,
                retailerId: 1
            );

        inactiveProduct.IsActive = false;

        var wrongRetailerProduct =
            CreateRetailerProduct(
                id: 101,
                itemId: item.Id,
                retailerId: 2
            );

        var validProduct =
            CreateRetailerProduct(
                id: 102,
                itemId: item.Id,
                retailerId: 1
            );

        SetupStoreAndList(
            20,
            store,
            new List<GroceryListItem>
            {
                listItem
            }
        );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(
                new List<RetailerProduct>
                {
                    inactiveProduct,
                    wrongRetailerProduct,
                    validProduct
                }
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    validProduct.Id,
                    store.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    4.00m
                )
            );

        // Act
        var result =
            await _service.CompareStoreAsync(
                20,
                store.Id
            );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsComplete);

        Assert.Equal(
            validProduct.Id,
            result.Items[0]
                .RetailerProductId
        );

        _priceServiceMock.Verify(
            service =>
                service.GetEffectivePriceAsync(
                    20,
                    inactiveProduct.Id,
                    store.Id
                ),
            Times.Never
        );

        _priceServiceMock.Verify(
            service =>
                service.GetEffectivePriceAsync(
                    20,
                    wrongRetailerProduct.Id,
                    store.Id
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CompareStoreAsync_WhenNoRetailerProductExists_MarksStoreIncomplete()
    {
        // Arrange
        var store =
            CreateStore();

        var item =
            CreateItem();

        var listItem =
            CreateGroceryListItem(
                itemId: item.Id,
                quantity: 2
            );

        SetupStoreAndList(
            20,
            store,
            new List<GroceryListItem>
            {
                listItem
            }
        );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(
                new List<RetailerProduct>()
            );

        // Act
        var result =
            await _service.CompareStoreAsync(
                20,
                store.Id
            );

        // Assert
        Assert.NotNull(result);

        Assert.False(
            result.IsComplete
        );

        Assert.Equal(
            0m,
            result.KnownSubtotal
        );

        Assert.Equal(
            1,
            result.MissingItemCount
        );

        Assert.Single(
            result.Items
        );

        Assert.False(
            result.Items[0]
                .IsAvailable
        );

        Assert.Null(
            result.Items[0]
                .RetailerProductId
        );

        Assert.Null(
            result.Items[0]
                .UnitPrice
        );

        Assert.Null(
            result.Items[0]
                .LineTotal
        );
    }

    [Fact]
    public async Task CompareStoreAsync_WhenProductsHaveNoUsableEffectivePrice_MarksStoreIncomplete()
    {
        // Arrange
        var store =
            CreateStore();

        var item =
            CreateItem();

        var listItem =
            CreateGroceryListItem(
                itemId: item.Id
            );

        var product =
            CreateRetailerProduct(
                id: 100,
                itemId: item.Id,
                retailerId:
                    store.RetailerId
            );

        SetupStoreAndList(
            20,
            store,
            new List<GroceryListItem>
            {
                listItem
            }
        );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(
                new List<RetailerProduct>
                {
                    product
                }
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    product.Id,
                    store.Id
                )
            )
            .ReturnsAsync(
                new EffectivePriceResult
                {
                    Amount = null,

                    PriceType = null,

                    AvailabilityStatus =
                        AvailabilityStatus.Unavailable,

                    HasRetailerMembership =
                        false,

                    LastCheckedAt =
                        DateTime.UtcNow,

                    SourceProvider =
                        "TestProvider"
                }
            );

        // Act
        var result =
            await _service.CompareStoreAsync(
                20,
                store.Id
            );

        // Assert
        Assert.NotNull(result);

        Assert.False(
            result.IsComplete
        );

        Assert.Equal(
            1,
            result.MissingItemCount
        );

        Assert.False(
            result.Items[0]
                .IsAvailable
        );

        Assert.Equal(
            0m,
            result.KnownSubtotal
        );
    }

    [Fact]
    public async Task CompareStoreAsync_WhenSomeItemsAreMissing_KeepsKnownSubtotalButMarksIncomplete()
    {
        // Arrange
        var store =
            CreateStore();

        var milk =
            CreateItem(
                id: 1,
                name: "Milk"
            );

        var eggs =
            CreateItem(
                id: 2,
                name: "Eggs"
            );

        var milkListItem =
            CreateGroceryListItem(
                itemId: milk.Id,
                quantity: 2
            );

        var eggsListItem =
            CreateGroceryListItem(
                itemId: eggs.Id,
                quantity: 1
            );

        var milkProduct =
            CreateRetailerProduct(
                id: 100,
                itemId: milk.Id,
                retailerId:
                    store.RetailerId
            );

        SetupStoreAndList(
            20,
            store,
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

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    milk.Id
                )
            )
            .ReturnsAsync(
                new List<RetailerProduct>
                {
                    milkProduct
                }
            );

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    eggs.Id
                )
            )
            .ReturnsAsync(
                new List<RetailerProduct>()
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    milkProduct.Id,
                    store.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    4.00m
                )
            );

        // Act
        var result =
            await _service.CompareStoreAsync(
                20,
                store.Id
            );

        // Assert
        Assert.NotNull(result);

        Assert.False(
            result.IsComplete
        );

        // Milk = 2 × $4 = $8.
        // Eggs are missing and therefore
        // contribute nothing to KNOWN subtotal.
        Assert.Equal(
            8.00m,
            result.KnownSubtotal
        );

        Assert.Equal(
            1,
            result.MissingItemCount
        );

        Assert.Equal(
            2,
            result.Items.Count
        );
    }

    [Fact]
    public async Task CompareStoresAsync_RanksCompleteStoresBySubtotalAndLeavesIncompleteStoreUnranked()
    {
        // Arrange
        var item =
            CreateItem();

        var listItem =
            CreateGroceryListItem(
                itemId: item.Id
            );

        var storeA =
            CreateStore(
                id: 10,
                name: "Store A"
            );

        var storeB =
            CreateStore(
                id: 11,
                name: "Store B"
            );

        var incompleteStore =
            CreateStore(
                id: 12,
                name: "Store C"
            );

        var product =
            CreateRetailerProduct(
                id: 100,
                itemId: item.Id,
                retailerId: 1
            );

        SetupMultiStoreSharedData(
            userId: 20,
            item,
            listItem,
            product,
            storeA,
            storeB,
            incompleteStore
        );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    product.Id,
                    storeA.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    5.00m
                )
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    product.Id,
                    storeB.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    3.00m
                )
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    product.Id,
                    incompleteStore.Id
                )
            )
            .ReturnsAsync(
                (EffectivePriceResult?)null
            );

        // Act
        var result =
            await _service.CompareStoresAsync(
                20,
                new List<int>
                {
                    storeA.Id,
                    storeB.Id,
                    incompleteStore.Id
                }
            );

        // Assert
        Assert.Equal(
            2,
            result.CompleteStoreCount
        );

        Assert.Equal(
            1,
            result.IncompleteStoreCount
        );

        Assert.Equal(
            storeB.Id,
            result.Stores[0]
                .StoreLocationId
        );

        Assert.Equal(
            1,
            result.Stores[0]
                .Rank
        );

        Assert.Equal(
            storeA.Id,
            result.Stores[1]
                .StoreLocationId
        );

        Assert.Equal(
            2,
            result.Stores[1]
                .Rank
        );

        Assert.Equal(
            incompleteStore.Id,
            result.Stores[2]
                .StoreLocationId
        );

        Assert.False(
            result.Stores[2]
                .IsComplete
        );

        Assert.Null(
            result.Stores[2]
                .Rank
        );
    }

    [Fact]
    public async Task CompareStoresAsync_WhenTotalsTie_UsesCompetitionRanking()
    {
        // Arrange
        var item =
            CreateItem();

        var listItem =
            CreateGroceryListItem(
                itemId: item.Id
            );

        var storeA =
            CreateStore(
                id: 10,
                name: "Store A"
            );

        var storeB =
            CreateStore(
                id: 11,
                name: "Store B"
            );

        var storeC =
            CreateStore(
                id: 12,
                name: "Store C"
            );

        var product =
            CreateRetailerProduct(
                id: 100,
                itemId: item.Id,
                retailerId: 1
            );

        SetupMultiStoreSharedData(
            20,
            item,
            listItem,
            product,
            storeA,
            storeB,
            storeC
        );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    product.Id,
                    storeA.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    3.00m
                )
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    product.Id,
                    storeB.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    3.00m
                )
            );

        _priceServiceMock
            .Setup(service =>
                service.GetEffectivePriceAsync(
                    20,
                    product.Id,
                    storeC.Id
                )
            )
            .ReturnsAsync(
                CreateEffectivePrice(
                    5.00m
                )
            );

        // Act
        var result =
            await _service.CompareStoresAsync(
                20,
                new List<int>
                {
                    storeA.Id,
                    storeB.Id,
                    storeC.Id
                }
            );

        // Assert
        Assert.Equal(
            3,
            result.CompleteStoreCount
        );

        Assert.Equal(
            1,
            result.Stores[0].Rank
        );

        Assert.Equal(
            1,
            result.Stores[1].Rank
        );

        Assert.Equal(
            3,
            result.Stores[2].Rank
        );

        Assert.Equal(
            3.00m,
            result.Stores[0]
                .KnownSubtotal
        );

        Assert.Equal(
            3.00m,
            result.Stores[1]
                .KnownSubtotal
        );

        Assert.Equal(
            5.00m,
            result.Stores[2]
                .KnownSubtotal
        );
    }

    [Fact]
    public async Task CompareStoresAsync_WhenStoreIdDoesNotExist_ReportsMissingStoreId()
    {
        // Arrange
        var realStore =
            CreateStore(
                id: 10
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    realStore.Id
                )
            )
            .ReturnsAsync(realStore);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    999
                )
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

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
            await _service.CompareStoresAsync(
                20,
                new List<int>
                {
                    realStore.Id,
                    999
                }
            );

        // Assert
        Assert.Single(
            result.MissingStoreLocationIds
        );

        Assert.Contains(
            999,
            result.MissingStoreLocationIds
        );

        Assert.DoesNotContain(
            999,
            result.Stores
                .Select(store =>
                    store.StoreLocationId
                )
        );
    }

    [Fact]
    public async Task CompareStoresAsync_WhenStoreIdsContainDuplicates_ProcessesStoreOnce()
    {
        // Arrange
        var store =
            CreateStore(
                id: 10
            );

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.Id
                )
            )
            .ReturnsAsync(store);

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
            await _service.CompareStoresAsync(
                20,
                new List<int>
                {
                    10,
                    10,
                    10
                }
            );

        // Assert
        Assert.Single(
            result.Stores
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    10
                ),
            Times.Once
        );
    }

    // ----------------------------------------------------
    // HELPERS
    // ----------------------------------------------------

    private void SetupStoreAndList(
        int userId,
        StoreLocation store,
        List<GroceryListItem> listItems)
    {
        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.Id
                )
            )
            .ReturnsAsync(store);

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.GetByUserIdAsync(
                    userId
                )
            )
            .ReturnsAsync(listItems);
    }

    private void SetupMultiStoreSharedData(
        int userId,
        Item item,
        GroceryListItem listItem,
        RetailerProduct product,
        params StoreLocation[] stores)
    {
        foreach (var store in stores)
        {
            _storeLocationRepositoryMock
                .Setup(repository =>
                    repository.GetByIdAsync(
                        store.Id
                    )
                )
                .ReturnsAsync(store);
        }

        _groceryListRepositoryMock
            .Setup(repository =>
                repository.GetByUserIdAsync(
                    userId
                )
            )
            .ReturnsAsync(
                new List<GroceryListItem>
                {
                    listItem
                }
            );

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _retailerProductRepositoryMock
            .Setup(repository =>
                repository.GetByItemIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(
                new List<RetailerProduct>
                {
                    product
                }
            );
    }

    private static EffectivePriceResult
        CreateEffectivePrice(
            decimal amount,
            EffectivePriceType type =
                EffectivePriceType.Regular)
    {
        return new EffectivePriceResult
        {
            Amount =
                amount,

            PriceType =
                type,

            AvailabilityStatus =
                AvailabilityStatus.Available,

            HasRetailerMembership =
                false,

            LastCheckedAt =
                DateTime.UtcNow,

            SourceProvider =
                "TestProvider"
        };
    }

    private static StoreLocation
        CreateStore(
            int id = 10,
            int retailerId = 1,
            string name = "Test Store")
    {
        return new StoreLocation
        {
            Id =
                id,

            RetailerId =
                retailerId,

            ExternalLocationId =
                $"STORE-{id}",

            Name =
                name,

            AddressLine1 =
                "123 Test Street",

            City =
                "Raleigh",

            State =
                "NC",

            PostalCode =
                "27606",

            IsActive =
                true
        };
    }

    private static Item CreateItem(
        int id = 1,
        string name = "Whole Milk")
    {
        return new Item
        {
            Id =
                id,

            Name =
                name,

            Size =
                "1 gallon",

            IsActive =
                true
        };
    }

    private static GroceryListItem
        CreateGroceryListItem(
            int itemId,
            int quantity = 1)
    {
        return new GroceryListItem
        {
            Id =
                itemId,

            UserId =
                20,

            ItemId =
                itemId,

            Quantity =
                quantity,

            CreatedAt =
                DateTime.UtcNow,

            UpdatedAt =
                DateTime.UtcNow
        };
    }

    private static RetailerProduct
        CreateRetailerProduct(
            int id,
            int itemId,
            int retailerId,
            string name = "Test Product")
    {
        return new RetailerProduct
        {
            Id =
                id,

            ItemId =
                itemId,

            RetailerId =
                retailerId,

            ExternalProductId =
                $"PRODUCT-{id}",

            Name =
                name,

            MatchMethod =
                ProductMatchMethod.Manual,

            IsActive =
                true,

            LastSeenAt =
                DateTime.UtcNow
        };
    }
}