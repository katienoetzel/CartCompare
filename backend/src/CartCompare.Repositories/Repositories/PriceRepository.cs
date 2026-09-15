using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Repositories.Repositories;

public class PriceRepository : IPriceRepository
{
    private readonly CartCompareDbContext _dbContext;

    public PriceRepository(CartCompareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Price?> GetByIdAsync(int id)
    {
        return await _dbContext.Prices
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Price?> GetByProductAndStoreAsync(
        int retailerProductId,
        int storeLocationId)
    {
        return await _dbContext.Prices
            .FirstOrDefaultAsync(p =>
                p.RetailerProductId == retailerProductId &&
                p.StoreLocationId == storeLocationId
            );
    }

    public async Task<List<Price>> GetByRetailerProductIdAsync(
        int retailerProductId)
    {
        return await _dbContext.Prices
            .Where(p =>
                p.RetailerProductId == retailerProductId
            )
            .OrderBy(p => p.StoreLocationId)
            .ToListAsync();
    }

    public async Task AddAsync(Price price)
    {
        await _dbContext.Prices.AddAsync(price);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Price price)
    {
        _dbContext.Prices.Update(price);
        await _dbContext.SaveChangesAsync();
    }
}