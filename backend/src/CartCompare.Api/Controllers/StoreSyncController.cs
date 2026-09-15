using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/store-sync")]
[Authorize]
public class StoreSyncController : ControllerBase
{
    private readonly IStoreLocationSyncService
        _storeLocationSyncService;

    public StoreSyncController(
        IStoreLocationSyncService storeLocationSyncService)
    {
        _storeLocationSyncService =
            storeLocationSyncService;
    }

    [HttpPost]
    public async Task<IActionResult> Sync(
        SyncStoreLocationsRequest request)
    {
        if (
            request.RetailerId <= 0 ||
            string.IsNullOrWhiteSpace(
                request.PostalCode
            ))
        {
            return BadRequest(new
            {
                message =
                    "Retailer ID and postal code are required."
            });
        }

        var result =
            await _storeLocationSyncService.SyncAsync(
                request.RetailerId,
                request.PostalCode
            );

        return result.Result switch
        {
            StoreLocationSyncResultType.Succeeded =>
                Ok(result),

            StoreLocationSyncResultType.RetailerNotFound =>
                NotFound(new
                {
                    message = "Retailer not found."
                }),

            StoreLocationSyncResultType.ProviderNotFound =>
                BadRequest(new
                {
                    message =
                        "No price provider supports this retailer."
                }),

            _ => StatusCode(500)
        };
    }
}