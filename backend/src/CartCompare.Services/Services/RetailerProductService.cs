using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class RetailerProductService
    : IRetailerProductService
{
    private readonly IRetailerProductRepository
        _retailerProductRepository;

    private readonly IRetailerRepository _retailerRepository;
    private readonly IItemRepository _itemRepository;

    public RetailerProductService(
        IRetailerProductRepository retailerProductRepository,
        IRetailerRepository retailerRepository,
        IItemRepository itemRepository)
    {
        _retailerProductRepository = retailerProductRepository;
        _retailerRepository = retailerRepository;
        _itemRepository = itemRepository;
    }

    public async Task<RetailerProduct?> GetByIdAsync(int id)
    {
        return await _retailerProductRepository.GetByIdAsync(id);
    }

    public async Task<List<RetailerProduct>>
        GetByRetailerIdAsync(int retailerId)
    {
        return await _retailerProductRepository
            .GetByRetailerIdAsync(retailerId);
    }

    public async Task<List<RetailerProduct>>
        GetByItemIdAsync(int itemId)
    {
        return await _retailerProductRepository
            .GetByItemIdAsync(itemId);
    }

    public async Task<CreateRetailerProductResult> CreateAsync(
        int itemId,
        int retailerId,
        string externalProductId,
        string name,
        string? brand,
        string? size,
        string? upc)
    {
        var retailer =
            await _retailerRepository.GetByIdAsync(retailerId);

        if (retailer is null)
        {
            return new CreateRetailerProductResult
            {
                Result =
                    RetailerProductCreateResult.RetailerNotFound
            };
        }

        var item =
            await _itemRepository.GetByIdAsync(itemId);

        if (item is null)
        {
            return new CreateRetailerProductResult
            {
                Result =
                    RetailerProductCreateResult.ItemNotFound
            };
        }

        var normalizedExternalId =
            externalProductId.Trim();

        var existingProduct =
            await _retailerProductRepository
                .GetByRetailerAndExternalIdAsync(
                    retailerId,
                    normalizedExternalId
                );

        if (existingProduct is not null)
        {
            return new CreateRetailerProductResult
            {
                Result =
                    RetailerProductCreateResult.AlreadyExists,

                RetailerProduct = existingProduct
            };
        }

        var retailerProduct = new RetailerProduct
        {
            ItemId = itemId,
            RetailerId = retailerId,
            ExternalProductId = normalizedExternalId,
            Name = name.Trim(),
            Brand = brand?.Trim(),
            Size = size?.Trim(),
            Upc = upc?.Trim(),
            MatchMethod = ProductMatchMethod.Manual,
            MatchConfidence = null,
            IsActive = true,
            LastSeenAt = DateTime.UtcNow
        };

        await _retailerProductRepository.AddAsync(
            retailerProduct
        );

        return new CreateRetailerProductResult
        {
            Result = RetailerProductCreateResult.Created,
            RetailerProduct = retailerProduct
        };
    }
}