using CartCompare.Entities;

namespace CartCompare.Services.Interfaces;

public interface IGroceryListService
{
    Task<List<GroceryListItem>> GetItemsAsync(int userId);

    Task<GroceryListItem?> SetQuantityAsync(
        int userId,
        int itemId,
        int quantity
    );

    Task<bool> RemoveItemAsync(
        int userId,
        int itemId
    );
}