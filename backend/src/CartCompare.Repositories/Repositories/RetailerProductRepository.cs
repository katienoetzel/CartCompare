using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Repositories.Repositories;

public class RetailerProductRepository
    : IRetailerProductRepository
{
    private readonly CartCompareDbContext _dbContext;

    public RetailerProductRepository(
        CartCompareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RetailerProduct?> GetByIdAsync(int id)
    {
        return await _dbContext.RetailerProducts
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<RetailerProduct>>
        GetByRetailerIdAsync(int retailerId)
    {
        return await _dbContext.RetailerProducts
            .Where(p =>
                p.RetailerId == retailerId &&
                p.IsActive
            )
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<RetailerProduct>>
        GetByItemIdAsync(int itemId)
    {
        return await _dbContext.RetailerProducts
            .Where(p =>
                p.ItemId == itemId &&
                p.IsActive
            )
            .OrderBy(p => p.RetailerId)
            .ToListAsync();
    }

    public async Task<RetailerProduct?>
        GetByRetailerAndExternalIdAsync(
            int retailerId,
            string externalProductId)
    {
        return await _dbContext.RetailerProducts
            .FirstOrDefaultAsync(p =>
                p.RetailerId == retailerId &&
                p.ExternalProductId == externalProductId
            );
    }

    public async Task AddAsync(
        RetailerProduct retailerProduct)
    {
        await _dbContext.RetailerProducts
            .AddAsync(retailerProduct);

        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(
    RetailerProduct retailerProduct)
    {
        _dbContext.RetailerProducts.Update(
            retailerProduct
        );

        await _dbContext.SaveChangesAsync();
    }
}