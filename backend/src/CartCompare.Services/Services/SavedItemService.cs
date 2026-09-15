using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class SavedItemService : ISavedItemService
{
    private readonly ISavedItemRepository _savedItemRepository;
    private readonly IItemRepository _itemRepository;

    public SavedItemService(
        ISavedItemRepository savedItemRepository,
        IItemRepository itemRepository)
    {
        _savedItemRepository = savedItemRepository;
        _itemRepository = itemRepository;
    }

    public async Task<List<SavedItemDetails>>
        GetSavedItemsAsync(int userId)
    {
        var savedItems =
            await _savedItemRepository.GetByUserIdAsync(userId);

        var results = new List<SavedItemDetails>();

        foreach (var savedItem in savedItems)
        {
            var item =
                await _itemRepository.GetByIdAsync(
                    savedItem.ItemId
                );

            if (item is null)
            {
                continue;
            }

            results.Add(
                CreateDetails(savedItem, item)
            );
        }

        return results;
    }

    public async Task<SavedItemDetails?> SaveItemAsync(
        int userId,
        int itemId)
    {
        var item =
            await _itemRepository.GetByIdAsync(itemId);

        if (item is null)
        {
            return null;
        }

        var existingSavedItem =
            await _savedItemRepository
                .GetByUserAndItemAsync(
                    userId,
                    itemId
                );

        if (existingSavedItem is not null)
        {
            return CreateDetails(
                existingSavedItem,
                item
            );
        }

        var savedItem = new SavedItem
        {
            UserId = userId,
            ItemId = itemId,
            CreatedAt = DateTime.UtcNow
        };

        await _savedItemRepository.AddAsync(savedItem);

        return CreateDetails(savedItem, item);
    }

    public async Task<bool> RemoveSavedItemAsync(
        int userId,
        int itemId)
    {
        var savedItem =
            await _savedItemRepository
                .GetByUserAndItemAsync(
                    userId,
                    itemId
                );

        if (savedItem is null)
        {
            return false;
        }

        await _savedItemRepository.RemoveAsync(savedItem);

        return true;
    }

    private static SavedItemDetails CreateDetails(
        SavedItem savedItem,
        Item item)
    {
        return new SavedItemDetails
        {
            SavedItemId = savedItem.Id,
            ItemId = item.Id,
            ItemName = item.Name,
            Brand = item.Brand,
            Size = item.Size,
            Category = item.Category,
            CreatedAt = savedItem.CreatedAt
        };
    }
}