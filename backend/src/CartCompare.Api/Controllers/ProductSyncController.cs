using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/product-sync")]
[Authorize]
public class ProductSyncController : ControllerBase
{
    private readonly IProductPriceSyncService
        _productPriceSyncService;

    public ProductSyncController(
        IProductPriceSyncService productPriceSyncService)
    {
        _productPriceSyncService =
            productPriceSyncService;
    }

    [HttpPost]
    public async Task<IActionResult> Sync(
        SyncProductPriceRequest request)
    {
        if (
            request.ItemId <= 0 ||
            request.StoreLocationId <= 0 ||
            string.IsNullOrWhiteSpace(
                request.ExternalProductId
            ))
        {
            return BadRequest(new
            {
                message =
                    "Item ID, store location ID, and external product ID are required."
            });
        }

        var result =
            await _productPriceSyncService.SyncAsync(
                request.ItemId,
                request.StoreLocationId,
                request.ExternalProductId
            );

        return result.Result switch
        {
            ProductPriceSyncResultType.Succeeded =>
                Ok(result),

            ProductPriceSyncResultType.ItemNotFound =>
                NotFound(new
                {
                    message = "Item not found."
                }),

            ProductPriceSyncResultType
                .StoreLocationNotFound =>
                NotFound(new
                {
                    message =
                        "Store location not found."
                }),

            ProductPriceSyncResultType.RetailerNotFound =>
                NotFound(new
                {
                    message = "Retailer not found."
                }),

            ProductPriceSyncResultType.ProviderNotFound =>
                BadRequest(new
                {
                    message =
                        "No provider supports this retailer."
                }),

            ProductPriceSyncResultType
                .ProviderProductNotFound =>
                NotFound(new
                {
                    message =
                        "The provider product was not found."
                }),

            ProductPriceSyncResultType
                .ProductMatchedToDifferentItem =>
                Conflict(new
                {
                    message =
                        "This retailer product is already matched to a different canonical item."
                }),

            ProductPriceSyncResultType
                .PriceNotAvailable =>
                Ok(result),

            ProductPriceSyncResultType
                .PriceSyncFailed =>
                StatusCode(500, result),

            _ =>
                StatusCode(500)
        };
    }
}