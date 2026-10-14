using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IGroceryListService
{
    Task<List<GroceryListItemDetails>> GetItemsAsync(
        int userId
    );

    // A list holds at most this many different items, which bounds
    // the retailer lookups one comparison can cause.
    const int MaxItems = 50;

    Task<GroceryListSetResult> SetQuantityAsync(
        int userId,
        int itemId,
        int quantity
    );

    Task<bool> RemoveItemAsync(
        int userId,
        int itemId
    );
}