using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IPriceService
{
    Task<Price?> GetByIdAsync(int id);

    Task<Price?> GetByProductAndStoreAsync(
        int retailerProductId,
        int storeLocationId
    );

    Task<List<Price>> GetByRetailerProductIdAsync(
        int retailerProductId
    );

    Task<PriceUpsertResult> UpsertAsync(
        int retailerProductId,
        int storeLocationId,
        decimal? regularPrice,
        decimal? salePrice,
        decimal? memberPrice,
        AvailabilityStatus availabilityStatus,
        string sourceProvider,
        DateTime? sourceUpdatedAt
    );

    Task<EffectivePriceResult?> GetEffectivePriceAsync(
    int userId,
    int retailerProductId,
    int storeLocationId
);
}