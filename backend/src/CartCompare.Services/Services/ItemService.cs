using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;

namespace CartCompare.Services.Services;

public class ItemService : IItemService
{
    private readonly IItemRepository _itemRepository;

    public ItemService(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<List<Item>> GetActiveAsync()
    {
        return await _itemRepository.GetActiveAsync();
    }

    public async Task<Item?> GetByIdAsync(int id)
    {
        return await _itemRepository.GetByIdAsync(id);
    }

    public async Task<Item> CreateAsync(
        string name,
        string? brand,
        string? size,
        string? category)
    {
        var item = new Item
        {
            Name = name.Trim(),
            Brand = brand?.Trim(),
            Size = size?.Trim(),
            Category = category?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _itemRepository.AddAsync(item);

        return item;
    }
}