using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class StoreLocationService : IStoreLocationService
{
    private readonly IStoreLocationRepository
        _storeLocationRepository;

    private readonly IRetailerRepository _retailerRepository;

    public StoreLocationService(
        IStoreLocationRepository storeLocationRepository,
        IRetailerRepository retailerRepository)
    {
        _storeLocationRepository = storeLocationRepository;
        _retailerRepository = retailerRepository;
    }

    public async Task<List<StoreLocation>>
        GetByRetailerIdAsync(int retailerId)
    {
        return await _storeLocationRepository
            .GetByRetailerIdAsync(retailerId);
    }

    public async Task<StoreLocation?> GetByIdAsync(int id)
    {
        return await _storeLocationRepository.GetByIdAsync(id);
    }

    public async Task<CreateStoreLocationResult> CreateAsync(
        int retailerId,
        string externalLocationId,
        string? name,
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        decimal? latitude,
        decimal? longitude)
    {
        var retailer =
            await _retailerRepository.GetByIdAsync(retailerId);

        if (retailer is null)
        {
            return new CreateStoreLocationResult
            {
                Result =
                    StoreLocationCreateResult.RetailerNotFound
            };
        }

        var normalizedExternalId =
            externalLocationId.Trim();

        var existingStore =
            await _storeLocationRepository
                .GetByRetailerAndExternalIdAsync(
                    retailerId,
                    normalizedExternalId
                );

        if (existingStore is not null)
        {
            return new CreateStoreLocationResult
            {
                Result = StoreLocationCreateResult.AlreadyExists,
                StoreLocation = existingStore
            };
        }

        var storeLocation = new StoreLocation
        {
            RetailerId = retailerId,
            ExternalLocationId = normalizedExternalId,
            Name = name?.Trim(),
            AddressLine1 = addressLine1.Trim(),
            AddressLine2 = addressLine2?.Trim(),
            City = city.Trim(),
            State = state.Trim(),
            PostalCode = postalCode.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            IsActive = true,
            LastSeenAt = DateTime.UtcNow
        };

        await _storeLocationRepository.AddAsync(storeLocation);

        return new CreateStoreLocationResult
        {
            Result = StoreLocationCreateResult.Created,
            StoreLocation = storeLocation
        };
    }
}