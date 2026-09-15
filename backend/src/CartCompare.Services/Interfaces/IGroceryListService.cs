using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IGroceryListService
{
    Task<List<GroceryListItemDetails>> GetItemsAsync(
        int userId
    );

    Task<GroceryListItemDetails?> SetQuantityAsync(
        int userId,
        int itemId,
        int quantity
    );

    Task<bool> RemoveItemAsync(
        int userId,
        int itemId
    );
}