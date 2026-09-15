using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IStoreLocationSyncService
{
    Task<StoreLocationSyncResult> SyncAsync(
        int retailerId,
        string postalCode
    );
}