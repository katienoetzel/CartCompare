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

    // Replaces the item's descriptive fields.
    // A null or blank brand, size, or category clears it,
    // which turns a brand-specific item into a generic one.
    // Returns null when the item does not exist.
    Task<Item?> UpdateAsync(
        int id,
        string name,
        string? brand,
        string? size,
        string? category
    );
}