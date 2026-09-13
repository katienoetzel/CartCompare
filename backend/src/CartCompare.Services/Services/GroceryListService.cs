using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;

namespace CartCompare.Services.Services;

public class GroceryListService : IGroceryListService
{
    private readonly IGroceryListRepository _groceryListRepository;
    private readonly IItemRepository _itemRepository;

    public GroceryListService(
        IGroceryListRepository groceryListRepository,
        IItemRepository itemRepository)
    {
        _groceryListRepository = groceryListRepository;
        _itemRepository = itemRepository;
    }

    public async Task<List<GroceryListItem>> GetItemsAsync(int userId)
    {
        return await _groceryListRepository.GetByUserIdAsync(userId);
    }

    public async Task<GroceryListItem?> SetQuantityAsync(
        int userId,
        int itemId,
        int quantity)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);

        if (item is null)
        {
            return null;
        }

        var existingItem =
            await _groceryListRepository.GetByUserAndItemAsync(
                userId,
                itemId
            );

        if (existingItem is not null)
        {
            existingItem.Quantity = quantity;
            existingItem.UpdatedAt = DateTime.UtcNow;

            await _groceryListRepository.UpdateAsync(existingItem);

            return existingItem;
        }

        var groceryListItem = new GroceryListItem
        {
            UserId = userId,
            ItemId = itemId,
            Quantity = quantity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _groceryListRepository.AddAsync(groceryListItem);

        return groceryListItem;
    }

    public async Task<bool> RemoveItemAsync(
        int userId,
        int itemId)
    {
        var groceryListItem =
            await _groceryListRepository.GetByUserAndItemAsync(
                userId,
                itemId
            );

        if (groceryListItem is null)
        {
            return false;
        }

        await _groceryListRepository.RemoveAsync(groceryListItem);

        return true;
    }
}