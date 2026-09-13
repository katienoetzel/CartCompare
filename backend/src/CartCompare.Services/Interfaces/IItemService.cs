using CartCompare.Entities;

namespace CartCompare.Services.Interfaces;

public interface IItemService
{
    Task<List<Item>> GetActiveAsync();

    Task<Item?> GetByIdAsync(int id);

    Task<Item> CreateAsync(
        string name,
        string? brand,
        string? size,
        string? category
    );
}