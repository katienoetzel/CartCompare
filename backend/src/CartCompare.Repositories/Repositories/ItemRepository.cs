using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Repositories.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly CartCompareDbContext _dbContext;

    public ItemRepository(CartCompareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Item>> GetActiveAsync()
    {
        return await _dbContext.Items
            .Where(i => i.IsActive)
            .ToListAsync();
    }

    public async Task<Item?> GetByIdAsync(int id)
    {
        return await _dbContext.Items
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task AddAsync(Item item)
    {
        await _dbContext.Items.AddAsync(item);
        await _dbContext.SaveChangesAsync();
    }
}