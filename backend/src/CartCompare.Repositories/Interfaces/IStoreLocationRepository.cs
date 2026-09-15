using CartCompare.Entities;

namespace CartCompare.Repositories.Interfaces;

public interface IStoreLocationRepository
{
    Task<List<StoreLocation>> GetByRetailerIdAsync(int retailerId);

    Task<StoreLocation?> GetByIdAsync(int id);

    Task<StoreLocation?> GetByRetailerAndExternalIdAsync(
        int retailerId,
        string externalLocationId
    );

    Task AddAsync(StoreLocation storeLocation);
    Task UpdateAsync(StoreLocation storeLocation);
}