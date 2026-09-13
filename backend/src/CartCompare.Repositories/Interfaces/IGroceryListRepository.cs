using CartCompare.Entities;

namespace CartCompare.Repositories.Interfaces;

public interface IGroceryListRepository
{
    Task<List<GroceryListItem>> GetByUserIdAsync(int userId);

    Task<GroceryListItem?> GetByUserAndItemAsync(
        int userId,
        int itemId
    );

    Task AddAsync(GroceryListItem groceryListItem);

    Task UpdateAsync(GroceryListItem groceryListItem);

    Task RemoveAsync(GroceryListItem groceryListItem);
}