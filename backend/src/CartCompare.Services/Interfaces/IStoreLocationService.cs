using CartCompare.Entities;
using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IStoreLocationService
{
    Task<List<StoreLocation>> GetByRetailerIdAsync(
        int retailerId
    );

    Task<StoreLocation?> GetByIdAsync(int id);

    Task<CreateStoreLocationResult> CreateAsync(
        int retailerId,
        string externalLocationId,
        string? name,
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        decimal? latitude,
        decimal? longitude
    );
}