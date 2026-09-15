using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Repositories.Repositories;

public class StoreLocationRepository : IStoreLocationRepository
{
    private readonly CartCompareDbContext _dbContext;

    public StoreLocationRepository(CartCompareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<StoreLocation>> GetByRetailerIdAsync(
        int retailerId)
    {
        return await _dbContext.StoreLocations
            .Where(s =>
                s.RetailerId == retailerId &&
                s.IsActive
            )
            .OrderBy(s => s.City)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<StoreLocation?> GetByIdAsync(int id)
    {
        return await _dbContext.StoreLocations
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<StoreLocation?>
        GetByRetailerAndExternalIdAsync(
            int retailerId,
            string externalLocationId)
    {
        return await _dbContext.StoreLocations
            .FirstOrDefaultAsync(s =>
                s.RetailerId == retailerId &&
                s.ExternalLocationId == externalLocationId
            );
    }

    public async Task AddAsync(StoreLocation storeLocation)
    {
        await _dbContext.StoreLocations.AddAsync(storeLocation);
        await _dbContext.SaveChangesAsync();
    }
    public async Task UpdateAsync(
    StoreLocation storeLocation)
    {
        _dbContext.StoreLocations.Update(storeLocation);

        await _dbContext.SaveChangesAsync();
    }
}