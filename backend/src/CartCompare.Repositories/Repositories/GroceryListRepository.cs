using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Repositories.Repositories;

public class GroceryListRepository : IGroceryListRepository
{
    private readonly CartCompareDbContext _dbContext;

    public GroceryListRepository(CartCompareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<GroceryListItem>> GetByUserIdAsync(int userId)
    {
        return await _dbContext.GroceryListItems
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<GroceryListItem?> GetByUserAndItemAsync(
        int userId,
        int itemId)
    {
        return await _dbContext.GroceryListItems
            .FirstOrDefaultAsync(g =>
                g.UserId == userId &&
                g.ItemId == itemId
            );
    }

    public async Task AddAsync(GroceryListItem groceryListItem)
    {
        await _dbContext.GroceryListItems.AddAsync(groceryListItem);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(GroceryListItem groceryListItem)
    {
        _dbContext.GroceryListItems.Update(groceryListItem);
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveAsync(GroceryListItem groceryListItem)
    {
        _dbContext.GroceryListItems.Remove(groceryListItem);
        await _dbContext.SaveChangesAsync();
    }
}