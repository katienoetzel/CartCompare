using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Repositories.Repositories;

public class RetailerRepository : IRetailerRepository
{
    private readonly CartCompareDbContext _dbContext;

    public RetailerRepository(CartCompareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Retailer>> GetActiveAsync()
    {
        return await _dbContext.Retailers
            .Where(r => r.IsActive)
            .ToListAsync();
    }

    public async Task<Retailer?> GetByIdAsync(int id)
    {
        return await _dbContext.Retailers
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task AddAsync(Retailer retailer)
    {
        await _dbContext.Retailers.AddAsync(retailer);
        await _dbContext.SaveChangesAsync();
    }
}