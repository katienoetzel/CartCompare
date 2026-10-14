using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IProductPriceSyncService
{
    Task<ProductPriceSyncResult> SyncMatchedAsync(
        int itemId, int storeLocationId, string externalProductId);

    Task<ProductPriceSyncResult> SyncAsync(
        int itemId,
        int storeLocationId,
        string externalProductId
    );
}