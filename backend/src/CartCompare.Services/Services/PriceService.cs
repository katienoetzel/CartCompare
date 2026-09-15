using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class PriceService : IPriceService
{
    private readonly IPriceRepository _priceRepository;
    private readonly IRetailerProductRepository
        _retailerProductRepository;
    private readonly IStoreLocationRepository
        _storeLocationRepository;
    private readonly IUserRetailerMembershipRepository
    _membershipRepository;

    public PriceService(
    IPriceRepository priceRepository,
    IRetailerProductRepository retailerProductRepository,
    IStoreLocationRepository storeLocationRepository,
    IUserRetailerMembershipRepository membershipRepository)
    {
        _priceRepository = priceRepository;
        _retailerProductRepository = retailerProductRepository;
        _storeLocationRepository = storeLocationRepository;
        _membershipRepository = membershipRepository;
    }

    public async Task<Price?> GetByIdAsync(int id)
    {
        return await _priceRepository.GetByIdAsync(id);
    }

    public async Task<Price?> GetByProductAndStoreAsync(
        int retailerProductId,
        int storeLocationId)
    {
        return await _priceRepository
            .GetByProductAndStoreAsync(
                retailerProductId,
                storeLocationId
            );
    }

    public async Task<List<Price>> GetByRetailerProductIdAsync(
        int retailerProductId)
    {
        return await _priceRepository
            .GetByRetailerProductIdAsync(retailerProductId);
    }

    public async Task<PriceUpsertResult> UpsertAsync(
        int retailerProductId,
        int storeLocationId,
        decimal? regularPrice,
        decimal? salePrice,
        decimal? memberPrice,
        AvailabilityStatus availabilityStatus,
        string sourceProvider,
        DateTime? sourceUpdatedAt)
    {
        if (
            regularPrice < 0 ||
            salePrice < 0 ||
            memberPrice < 0)
        {
            return new PriceUpsertResult
            {
                Result = PriceUpsertResultType.InvalidPrice
            };
        }

        var retailerProduct =
            await _retailerProductRepository.GetByIdAsync(
                retailerProductId
            );

        if (retailerProduct is null)
        {
            return new PriceUpsertResult
            {
                Result =
                    PriceUpsertResultType.RetailerProductNotFound
            };
        }

        var storeLocation =
            await _storeLocationRepository.GetByIdAsync(
                storeLocationId
            );

        if (storeLocation is null)
        {
            return new PriceUpsertResult
            {
                Result =
                    PriceUpsertResultType.StoreLocationNotFound
            };
        }

        if (
            retailerProduct.RetailerId !=
            storeLocation.RetailerId)
        {
            return new PriceUpsertResult
            {
                Result =
                    PriceUpsertResultType.RetailerMismatch
            };
        }

        var existingPrice =
            await _priceRepository.GetByProductAndStoreAsync(
                retailerProductId,
                storeLocationId
            );

        var now = DateTime.UtcNow;

        if (existingPrice is not null)
        {
            existingPrice.RegularPrice = regularPrice;
            existingPrice.SalePrice = salePrice;
            existingPrice.MemberPrice = memberPrice;
            existingPrice.AvailabilityStatus =
                availabilityStatus;
            existingPrice.SourceProvider =
                sourceProvider.Trim();
            existingPrice.SourceUpdatedAt =
                sourceUpdatedAt;
            existingPrice.LastCheckedAt = now;
            existingPrice.UpdatedAt = now;

            await _priceRepository.UpdateAsync(existingPrice);

            return new PriceUpsertResult
            {
                Result = PriceUpsertResultType.Updated,
                StoredPrice = existingPrice
            };
        }

        var price = new Price
        {
            RetailerProductId = retailerProductId,
            StoreLocationId = storeLocationId,
            RegularPrice = regularPrice,
            SalePrice = salePrice,
            MemberPrice = memberPrice,
            AvailabilityStatus = availabilityStatus,
            SourceProvider = sourceProvider.Trim(),
            SourceUpdatedAt = sourceUpdatedAt,
            LastCheckedAt = now,
            UpdatedAt = now
        };

        await _priceRepository.AddAsync(price);

        return new PriceUpsertResult
        {
            Result = PriceUpsertResultType.Created,
            StoredPrice = price
        };
    }
    public async Task<EffectivePriceResult?> GetEffectivePriceAsync(
    int userId,
    int retailerProductId,
    int storeLocationId)
    {
        var price =
            await _priceRepository.GetByProductAndStoreAsync(
                retailerProductId,
                storeLocationId
            );

        if (price is null)
        {
            return null;
        }

        var retailerProduct =
            await _retailerProductRepository.GetByIdAsync(
                retailerProductId
            );

        if (retailerProduct is null)
        {
            return null;
        }

        var membership =
            await _membershipRepository.GetByUserAndRetailerAsync(
                userId,
                retailerProduct.RetailerId
            );

        var hasMembership = membership is not null;

        if (price.AvailabilityStatus ==
            AvailabilityStatus.Unavailable)
        {
            return new EffectivePriceResult
            {
                Amount = null,
                PriceType = null,
                AvailabilityStatus = price.AvailabilityStatus,
                HasRetailerMembership = hasMembership,
                LastCheckedAt = price.LastCheckedAt,
                SourceProvider = price.SourceProvider
            };
        }

        var candidates =
            new List<(decimal Amount, EffectivePriceType Type)>();

        if (price.RegularPrice is not null)
        {
            candidates.Add((
                price.RegularPrice.Value,
                EffectivePriceType.Regular
            ));
        }

        if (price.SalePrice is not null)
        {
            candidates.Add((
                price.SalePrice.Value,
                EffectivePriceType.Sale
            ));
        }

        if (
            hasMembership &&
            price.MemberPrice is not null)
        {
            candidates.Add((
                price.MemberPrice.Value,
                EffectivePriceType.Member
            ));
        }

        if (candidates.Count == 0)
        {
            return new EffectivePriceResult
            {
                Amount = null,
                PriceType = null,
                AvailabilityStatus = price.AvailabilityStatus,
                HasRetailerMembership = hasMembership,
                LastCheckedAt = price.LastCheckedAt,
                SourceProvider = price.SourceProvider
            };
        }

        var bestPrice =
            candidates.OrderBy(candidate => candidate.Amount)
                .First();

        return new EffectivePriceResult
        {
            Amount = bestPrice.Amount,
            PriceType = bestPrice.Type,
            AvailabilityStatus = price.AvailabilityStatus,
            HasRetailerMembership = hasMembership,
            LastCheckedAt = price.LastCheckedAt,
            SourceProvider = price.SourceProvider
        };
    }
}