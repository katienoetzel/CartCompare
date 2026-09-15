using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface ISavedItemService
{
    Task<List<SavedItemDetails>> GetSavedItemsAsync(
        int userId
    );

    Task<SavedItemDetails?> SaveItemAsync(
        int userId,
        int itemId
    );

    Task<bool> RemoveSavedItemAsync(
        int userId,
        int itemId
    );
}