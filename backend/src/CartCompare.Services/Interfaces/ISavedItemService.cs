using CartCompare.Entities;

namespace CartCompare.Services.Interfaces;

public interface ISavedItemService
{
    Task<List<SavedItem>> GetSavedItemsAsync(int userId);

    Task<SavedItem> SaveItemAsync(int userId, int itemId);

    Task<bool> RemoveSavedItemAsync(int userId, int itemId);
}