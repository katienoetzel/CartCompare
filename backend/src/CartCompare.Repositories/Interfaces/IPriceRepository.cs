using CartCompare.Entities;

namespace CartCompare.Repositories.Interfaces;

public interface IPriceRepository
{
    Task<Price?> GetByIdAsync(int id);

    Task<Price?> GetByProductAndStoreAsync(
        int retailerProductId,
        int storeLocationId
    );

    Task<List<Price>> GetByRetailerProductIdAsync(
        int retailerProductId
    );

    Task AddAsync(Price price);

    Task UpdateAsync(Price price);
}