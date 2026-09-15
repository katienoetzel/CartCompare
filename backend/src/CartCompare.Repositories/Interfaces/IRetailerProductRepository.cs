using CartCompare.Entities;

namespace CartCompare.Repositories.Interfaces;

public interface IRetailerProductRepository
{
    Task<RetailerProduct?> GetByIdAsync(int id);

    Task<List<RetailerProduct>> GetByRetailerIdAsync(
        int retailerId
    );

    Task<List<RetailerProduct>> GetByItemIdAsync(
        int itemId
    );

    Task<RetailerProduct?> GetByRetailerAndExternalIdAsync(
        int retailerId,
        string externalProductId
    );

    Task AddAsync(RetailerProduct retailerProduct);
    Task UpdateAsync(RetailerProduct retailerProduct);
}