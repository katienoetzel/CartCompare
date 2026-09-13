using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Repositories.Repositories;

public class SavedItemRepository : ISavedItemRepository
{
    private readonly CartCompareDbContext _dbContext;

    public SavedItemRepository(CartCompareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SavedItem>> GetByUserIdAsync(int userId)
    {
        return await _dbContext.SavedItems
            .Where(s => s.UserId == userId)
            .ToListAsync();
    }

    public async Task<SavedItem?> GetByUserAndItemAsync(
        int userId,
        int itemId)
    {
        return await _dbContext.SavedItems
            .FirstOrDefaultAsync(s =>
                s.UserId == userId &&
                s.ItemId == itemId
            );
    }

    public async Task AddAsync(SavedItem savedItem)
    {
        await _dbContext.SavedItems.AddAsync(savedItem);
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveAsync(SavedItem savedItem)
    {
        _dbContext.SavedItems.Remove(savedItem);
        await _dbContext.SaveChangesAsync();
    }
}