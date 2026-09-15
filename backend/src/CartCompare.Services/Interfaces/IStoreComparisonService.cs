using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IStoreComparisonService
{
    Task<StoreComparisonResult?> CompareStoreAsync(
        int userId,
        int storeLocationId
    );

    Task<MultiStoreComparisonResult> CompareStoresAsync(
        int userId,
        List<int> storeLocationIds
    );
}