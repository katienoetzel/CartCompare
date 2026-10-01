using CartCompare.Api.Models.Requests;
using CartCompare.Infrastructure.Providers.Kroger;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/store-locations")]
public class StoreLocationsController : ControllerBase
{
    private readonly IStoreLocationService
        _storeLocationService;

    private readonly IRetailerService _retailerService;
    private readonly IStoreLocationSyncService _storeLocationSyncService;

    public StoreLocationsController(
        IStoreLocationService storeLocationService,
        IRetailerService retailerService,
        IStoreLocationSyncService storeLocationSyncService)
    {
        _storeLocationService =
            storeLocationService;

        _retailerService = retailerService;
        _storeLocationSyncService = storeLocationSyncService;
    }

    // Discovery refreshes store records, so this is a POST rather than a GET.
    // The returned IDs can be passed directly to the comparison endpoints.
    [Authorize]
    [HttpPost("search")]
    public async Task<IActionResult> SearchNearby(
        SearchNearbyStoresRequest request)
    {
        var postalCode = request.PostalCode?.Trim();

        if (postalCode is null ||
            postalCode.Length != 5 ||
            !postalCode.All(char.IsAsciiDigit))
        {
            return BadRequest(new
            {
                message = "A five-digit US postal code is required."
            });
        }

        var stores = new List<object>();
        var failedRetailers = new List<object>();
        var retailers = await _retailerService.GetActiveAsync();

        foreach (var retailer in retailers)
        {
            StoreLocationSyncResult sync;

            try
            {
                sync = await _storeLocationSyncService.SyncAsync(
                    retailer.Id,
                    postalCode
                );
            }
            catch (HttpRequestException exception)
            {
                failedRetailers.Add(new
                {
                    retailer.Id,
                    retailer.Name,
                    reason = exception.StatusCode is null
                        ? "network_error"
                        : $"http_{(int)exception.StatusCode.Value}",
                    stage = (exception as KrogerApiException)?.Stage
                });
                continue;
            }
            catch (TaskCanceledException)
            {
                failedRetailers.Add(new
                {
                    retailer.Id,
                    retailer.Name,
                    reason = "timeout"
                });
                continue;
            }

            if (sync.Result == StoreLocationSyncResultType.ProviderNotFound)
            {
                continue;
            }

            if (sync.Result != StoreLocationSyncResultType.Succeeded)
            {
                failedRetailers.Add(new
                {
                    retailer.Id,
                    retailer.Name,
                    reason = "sync_failed"
                });
                continue;
            }

            var currentStoreIds = sync.SyncedStoreLocationIds.ToHashSet();
            var savedStores = await _storeLocationService
                .GetByRetailerIdAsync(retailer.Id);

            stores.AddRange(savedStores
                .Where(store => currentStoreIds.Contains(store.Id))
                .Select(store => (object)new
                {
                    store.Id,
                    store.RetailerId,
                    retailerName = retailer.Name,
                    store.ExternalLocationId,
                    store.Name,
                    store.AddressLine1,
                    store.AddressLine2,
                    store.City,
                    store.State,
                    store.PostalCode,
                    store.Latitude,
                    store.Longitude
                }));
        }

        return Ok(new
        {
            postalCode,
            stores,
            failedRetailers
        });
    }

    [HttpGet("retailer/{retailerId:int}")]
    public async Task<IActionResult> GetByRetailer(
        int retailerId)
    {
        var stores =
            await _storeLocationService
                .GetByRetailerIdAsync(
                    retailerId
                );

        return Ok(stores);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id)
    {
        var store =
            await _storeLocationService
                .GetByIdAsync(
                    id
                );

        if (store is null)
        {
            return NotFound();
        }

        return Ok(store);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateStoreLocationRequest request)
    {
        var result =
            await _storeLocationService
                .CreateAsync(
                    request.RetailerId,
                    request.ExternalLocationId,
                    request.Name,
                    request.AddressLine1,
                    request.AddressLine2,
                    request.City,
                    request.State,
                    request.PostalCode,
                    request.Latitude,
                    request.Longitude
                );

        return result.Result switch
        {
            StoreLocationCreateResult
                .Created =>
                Ok(result.StoreLocation),

            StoreLocationCreateResult
                .AlreadyExists =>
                Ok(result.StoreLocation),

            StoreLocationCreateResult
                .RetailerNotFound =>
                NotFound(new
                {
                    message =
                        "Retailer not found."
                }),

            _ =>
                StatusCode(500)
        };
    }
}
