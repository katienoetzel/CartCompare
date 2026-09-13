using CartCompare.Entities;

namespace CartCompare.Repositories.Interfaces;

public interface ISavedItemRepository
{
    Task<List<SavedItem>> GetByUserIdAsync(int userId);

    Task<SavedItem?> GetByUserAndItemAsync(int userId, int itemId);

    Task AddAsync(SavedItem savedItem);

    Task RemoveAsync(SavedItem savedItem);
}