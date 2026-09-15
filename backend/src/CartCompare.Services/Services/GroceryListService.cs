using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class GroceryListService : IGroceryListService
{
    private readonly IGroceryListRepository
        _groceryListRepository;

    private readonly IItemRepository
        _itemRepository;

    public GroceryListService(
        IGroceryListRepository groceryListRepository,
        IItemRepository itemRepository)
    {
        _groceryListRepository = groceryListRepository;
        _itemRepository = itemRepository;
    }

    public async Task<List<GroceryListItemDetails>>
        GetItemsAsync(int userId)
    {
        var groceryListItems =
            await _groceryListRepository.GetByUserIdAsync(
                userId
            );

        var results =
            new List<GroceryListItemDetails>();

        foreach (var groceryListItem in groceryListItems)
        {
            var item =
                await _itemRepository.GetByIdAsync(
                    groceryListItem.ItemId
                );

            if (item is null)
            {
                continue;
            }

            results.Add(
                CreateDetails(
                    groceryListItem,
                    item
                )
            );
        }

        return results;
    }

    public async Task<GroceryListItemDetails?>
        SetQuantityAsync(
            int userId,
            int itemId,
            int quantity)
    {
        var item =
            await _itemRepository.GetByIdAsync(itemId);

        if (item is null)
        {
            return null;
        }

        var existingItem =
            await _groceryListRepository
                .GetByUserAndItemAsync(
                    userId,
                    itemId
                );

        if (existingItem is not null)
        {
            existingItem.Quantity = quantity;
            existingItem.UpdatedAt = DateTime.UtcNow;

            await _groceryListRepository.UpdateAsync(
                existingItem
            );

            return CreateDetails(
                existingItem,
                item
            );
        }

        var groceryListItem = new GroceryListItem
        {
            UserId = userId,
            ItemId = itemId,
            Quantity = quantity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _groceryListRepository.AddAsync(
            groceryListItem
        );

        return CreateDetails(
            groceryListItem,
            item
        );
    }

    public async Task<bool> RemoveItemAsync(
        int userId,
        int itemId)
    {
        var groceryListItem =
            await _groceryListRepository
                .GetByUserAndItemAsync(
                    userId,
                    itemId
                );

        if (groceryListItem is null)
        {
            return false;
        }

        await _groceryListRepository.RemoveAsync(
            groceryListItem
        );

        return true;
    }

    private static GroceryListItemDetails CreateDetails(
        GroceryListItem groceryListItem,
        Item item)
    {
        return new GroceryListItemDetails
        {
            GroceryListItemId = groceryListItem.Id,
            ItemId = item.Id,
            ItemName = item.Name,
            Brand = item.Brand,
            Size = item.Size,
            Category = item.Category,
            Quantity = groceryListItem.Quantity,
            CreatedAt = groceryListItem.CreatedAt,
            UpdatedAt = groceryListItem.UpdatedAt
        };
    }
}