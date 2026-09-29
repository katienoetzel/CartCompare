using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Infrastructure.Providers.LowesFoods;
using CartCompare.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/dev/lowes-foods")]
[AllowAnonymous]
public sealed class LowesFoodsDevelopmentController
    : ControllerBase
{
    private const string RetailerName =
        "Lowes Foods";

    private const string GarnerLocationId =
        "4rw2mRMnac9YV56NM4GY1N";

    private readonly IWebHostEnvironment
        _environment;

    private readonly CartCompareDbContext
        _dbContext;

    private readonly LowesFoodsPriceProvider
        _lowesFoodsPriceProvider;

    private readonly IProductPriceSyncService
        _productPriceSyncService;

    public LowesFoodsDevelopmentController(
        IWebHostEnvironment environment,
        CartCompareDbContext dbContext,
        LowesFoodsPriceProvider lowesFoodsPriceProvider,
        IProductPriceSyncService productPriceSyncService)
    {
        _environment =
            environment;

        _dbContext =
            dbContext;

        _lowesFoodsPriceProvider =
            lowesFoodsPriceProvider;

        _productPriceSyncService =
            productPriceSyncService;
    }

    [HttpPost("bootstrap-and-sync")]
    public async Task<IActionResult>
        BootstrapAndSync()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var retailer =
            await EnsureRetailerAsync();

        var store =
            await EnsureGarnerStoreAsync(
                retailer.Id
            );

        var item =
            await EnsureWholeMilkItemAsync();

        var products =
            await _lowesFoodsPriceProvider
                .SearchProductsAsync(
                    RetailerName,
                    GarnerLocationId,
                    "whole milk"
                );

        var candidate =
            products.FirstOrDefault(
                product =>
                    string.Equals(
                        product.ExternalProductId,
                        "38322",
                        StringComparison
                            .OrdinalIgnoreCase
                    )
            )
            ??
            products.FirstOrDefault(
                product =>
                    product.Name.Contains(
                        "Lowes Foods Vitamin D Whole Milk 1 gal",
                        StringComparison
                            .OrdinalIgnoreCase
                    )
            );

        if (candidate is null)
        {
            return StatusCode(
                StatusCodes
                    .Status502BadGateway,
                new
                {
                    message =
                        "Lowes Foods search succeeded, but the expected 1-gallon whole milk product was not found.",

                    providerProductCount =
                        products.Count
                }
            );
        }

        var syncResult =
            await _productPriceSyncService
                .SyncAsync(
                    item.Id,
                    store.Id,
                    candidate.ExternalProductId
                );

        var storedProduct =
            await _dbContext
                .RetailerProducts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    product =>
                        product.RetailerId
                            ==
                            retailer.Id
                        &&
                        product.ExternalProductId
                            ==
                            candidate.ExternalProductId
                );

        Price? storedPrice =
            null;

        if (storedProduct is not null)
        {
            storedPrice =
                await _dbContext
                    .Prices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        price =>
                            price.RetailerProductId
                                ==
                                storedProduct.Id
                            &&
                            price.StoreLocationId
                                ==
                                store.Id
                    );
        }

        return Ok(new
        {
            retailer =
                new
                {
                    retailer.Id,
                    retailer.Name
                },

            store =
                new
                {
                    store.Id,
                    store.ExternalLocationId,
                    store.Name,
                    store.AddressLine1,
                    store.City,
                    store.State,
                    store.PostalCode
                },

            item =
                new
                {
                    item.Id,
                    item.Name,
                    item.Size
                },

            providerProductCount =
                products.Count,

            candidate,

            sync =
                new
                {
                    result =
                        syncResult.Result
                            .ToString(),

                    syncResult.ProductCreated,
                    syncResult.ProductUpdated,
                    syncResult.PriceSynced,
                    syncResult.ProviderName
                },

            persisted =
                new
                {
                    retailerProductId =
                        storedProduct?.Id,

                    regularPrice =
                        storedPrice?.RegularPrice,

                    salePrice =
                        storedPrice?.SalePrice,

                    memberPrice =
                        storedPrice?.MemberPrice,

                    availabilityStatus =
                        storedPrice?
                            .AvailabilityStatus
                            .ToString(),

                    sourceProvider =
                        storedPrice?.SourceProvider,

                    sourceUpdatedAt =
                        storedPrice?.SourceUpdatedAt
                }
        });
    }

    private async Task<Retailer>
        EnsureRetailerAsync()
    {
        var existing =
            await _dbContext
                .Retailers
                .FirstOrDefaultAsync(
                    retailer =>
                        retailer.Name
                            ==
                            RetailerName
                );

        if (existing is not null)
        {
            existing.IsActive =
                true;

            await _dbContext
                .SaveChangesAsync();

            return existing;
        }

        var retailer =
            new Retailer
            {
                Name =
                    RetailerName,

                SupportsMembership =
                    false,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        _dbContext
            .Retailers
            .Add(retailer);

        await _dbContext
            .SaveChangesAsync();

        return retailer;
    }

    private async Task<StoreLocation>
        EnsureGarnerStoreAsync(
            int retailerId)
    {
        var existing =
            await _dbContext
                .StoreLocations
                .FirstOrDefaultAsync(
                    store =>
                        store.RetailerId
                            ==
                            retailerId
                        &&
                        store.ExternalLocationId
                            ==
                            GarnerLocationId
                );

        if (existing is not null)
        {
            existing.Name =
                "Lowes Foods #185 - Garner";

            existing.AddressLine1 =
                "1845 Aversboro Road";

            existing.AddressLine2 =
                null;

            existing.City =
                "Garner";

            existing.State =
                "NC";

            existing.PostalCode =
                "27529";

            existing.IsActive =
                true;

            existing.LastSeenAt =
                DateTime.UtcNow;

            await _dbContext
                .SaveChangesAsync();

            return existing;
        }

        var store =
            new StoreLocation
            {
                RetailerId =
                    retailerId,

                ExternalLocationId =
                    GarnerLocationId,

                Name =
                    "Lowes Foods #185 - Garner",

                AddressLine1 =
                    "1845 Aversboro Road",

                AddressLine2 =
                    null,

                City =
                    "Garner",

                State =
                    "NC",

                PostalCode =
                    "27529",

                Latitude =
                    null,

                Longitude =
                    null,

                IsActive =
                    true,

                LastSeenAt =
                    DateTime.UtcNow
            };

        _dbContext
            .StoreLocations
            .Add(store);

        await _dbContext
            .SaveChangesAsync();

        return store;
    }

    private async Task<Item>
        EnsureWholeMilkItemAsync()
    {
        var existing =
            await _dbContext
                .Items
                .FirstOrDefaultAsync(
                    item =>
                        item.Name
                            ==
                            "Whole Milk"
                        &&
                        item.Size
                            ==
                            "1 gallon"
                );

        if (existing is not null)
        {
            existing.IsActive =
                true;

            existing.UpdatedAt =
                DateTime.UtcNow;

            await _dbContext
                .SaveChangesAsync();

            return existing;
        }

        var now =
            DateTime.UtcNow;

        var item =
            new Item
            {
                Name =
                    "Whole Milk",

                Brand =
                    null,

                Size =
                    "1 gallon",

                Category =
                    "Dairy",

                IsActive =
                    true,

                CreatedAt =
                    now,

                UpdatedAt =
                    now
            };

        _dbContext
            .Items
            .Add(item);

        await _dbContext
            .SaveChangesAsync();

        return item;
    }
}
