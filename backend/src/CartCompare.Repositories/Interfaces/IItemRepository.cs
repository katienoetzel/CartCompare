using CartCompare.Entities;

namespace CartCompare.Repositories.Interfaces;

public interface IItemRepository
{
    Task<List<Item>> GetActiveAsync();

    Task<Item?> GetByIdAsync(int id);

    Task AddAsync(Item item);
}