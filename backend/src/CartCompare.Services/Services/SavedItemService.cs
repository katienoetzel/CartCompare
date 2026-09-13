using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;

namespace CartCompare.Services.Services;

public class SavedItemService : ISavedItemService
{
    private readonly ISavedItemRepository _savedItemRepository;

    public SavedItemService(ISavedItemRepository savedItemRepository)
    {
        _savedItemRepository = savedItemRepository;
    }

    public async Task<List<SavedItem>> GetSavedItemsAsync(int userId)
    {
        return await _savedItemRepository.GetByUserIdAsync(userId);
    }

    public async Task<SavedItem> SaveItemAsync(int userId, int itemId)
    {
        var existingSavedItem =
            await _savedItemRepository.GetByUserAndItemAsync(userId, itemId);

        if (existingSavedItem is not null)
        {
            return existingSavedItem;
        }

        var savedItem = new SavedItem
        {
            UserId = userId,
            ItemId = itemId,
            CreatedAt = DateTime.UtcNow
        };

        await _savedItemRepository.AddAsync(savedItem);

        return savedItem;
    }

    public async Task<bool> RemoveSavedItemAsync(int userId, int itemId)
    {
        var savedItem =
            await _savedItemRepository.GetByUserAndItemAsync(userId, itemId);

        if (savedItem is null)
        {
            return false;
        }

        await _savedItemRepository.RemoveAsync(savedItem);

        return true;
    }
}