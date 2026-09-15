using CartCompare.Entities;
using CartCompare.Providers.Interfaces;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class StoreLocationSyncService
    : IStoreLocationSyncService
{
    private readonly IRetailerRepository
        _retailerRepository;

    private readonly IStoreLocationRepository
        _storeLocationRepository;

    private readonly IPriceProviderResolver
        _providerResolver;

    public StoreLocationSyncService(
        IRetailerRepository retailerRepository,
        IStoreLocationRepository storeLocationRepository,
        IPriceProviderResolver providerResolver)
    {
        _retailerRepository = retailerRepository;
        _storeLocationRepository = storeLocationRepository;
        _providerResolver = providerResolver;
    }

    public async Task<StoreLocationSyncResult> SyncAsync(
        int retailerId,
        string postalCode)
    {
        var retailer =
            await _retailerRepository.GetByIdAsync(
                retailerId
            );

        if (retailer is null)
        {
            return new StoreLocationSyncResult
            {
                Result =
                    StoreLocationSyncResultType.RetailerNotFound,

                RetailerId = retailerId,
                RetailerName = "Unknown"
            };
        }

        var provider =
            _providerResolver.Resolve(retailer.Name);

        if (provider is null)
        {
            return new StoreLocationSyncResult
            {
                Result =
                    StoreLocationSyncResultType.ProviderNotFound,

                RetailerId = retailer.Id,
                RetailerName = retailer.Name
            };
        }

        var providerStores =
            await provider.FindStoresAsync(
                retailer.Name,
                postalCode.Trim()
            );

        var createdCount = 0;
        var updatedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var providerStore in providerStores)
        {
            var existingStore =
                await _storeLocationRepository
                    .GetByRetailerAndExternalIdAsync(
                        retailer.Id,
                        providerStore.ExternalLocationId
                    );

            if (existingStore is null)
            {
                var storeLocation =
                    new StoreLocation
                    {
                        RetailerId = retailer.Id,

                        ExternalLocationId =
                            providerStore.ExternalLocationId,

                        Name =
                            providerStore.Name,

                        AddressLine1 =
                            providerStore.AddressLine1,

                        AddressLine2 =
                            providerStore.AddressLine2,

                        City =
                            providerStore.City,

                        State =
                            providerStore.State,

                        PostalCode =
                            providerStore.PostalCode,

                        Latitude =
                            providerStore.Latitude,

                        Longitude =
                            providerStore.Longitude,

                        IsActive = true,
                        LastSeenAt = now
                    };

                await _storeLocationRepository
                    .AddAsync(storeLocation);

                createdCount++;

                continue;
            }

            existingStore.Name =
                providerStore.Name;

            existingStore.AddressLine1 =
                providerStore.AddressLine1;

            existingStore.AddressLine2 =
                providerStore.AddressLine2;

            existingStore.City =
                providerStore.City;

            existingStore.State =
                providerStore.State;

            existingStore.PostalCode =
                providerStore.PostalCode;

            existingStore.Latitude =
                providerStore.Latitude;

            existingStore.Longitude =
                providerStore.Longitude;

            existingStore.IsActive = true;
            existingStore.LastSeenAt = now;

            await _storeLocationRepository
                .UpdateAsync(existingStore);

            updatedCount++;
        }

        return new StoreLocationSyncResult
        {
            Result =
                StoreLocationSyncResultType.Succeeded,

            RetailerId = retailer.Id,
            RetailerName = retailer.Name,
            ProviderName = provider.ProviderName,

            ProviderStoreCount =
                providerStores.Count,

            CreatedCount =
                createdCount,

            UpdatedCount =
                updatedCount
        };
    }
}