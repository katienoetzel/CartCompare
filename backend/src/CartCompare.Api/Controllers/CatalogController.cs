using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CartCompare.Api.Controllers;

// Shoppers add missing grocery items by picking a real product
// from a nearby store's catalog.
[ApiController]
[Route("api/catalog")]
[EnableRateLimiting(RateLimitPolicies.RetailerSearch)]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(
        ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("products")]
    public async Task<IActionResult> SearchProducts(
        [FromQuery] int storeLocationId,
        [FromQuery] string? query)
    {
        var trimmed = query?.Trim() ?? "";

        if (storeLocationId <= 0 || trimmed.Length is < 2 or > 100)
        {
            return BadRequest(new
            {
                message = "Enter 2 to 100 characters to search."
            });
        }

        var result =
            await _catalogService.SearchAsync(
                storeLocationId,
                trimmed
            );

        return result.Result switch
        {
            CatalogResultType.Succeeded => Ok(new
            {
                storeLocationId,
                retailerName = result.RetailerName,
                products = result.Products
            }),
            CatalogResultType.StoreNotFound => NotFound(new
            {
                message = "Store not found."
            }),
            _ => BadRequest(new
            {
                message = $"Product search is not available for {result.RetailerName ?? "this retailer"}."
            })
        };
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem(
        CreateCatalogItemRequest request)
    {
        var result =
            await _catalogService.CreateItemFromProductAsync(
                request.StoreLocationId,
                request.ExternalProductId,
                request.Name,
                request.Size,
                request.Category
            );

        return result.Result switch
        {
            CatalogResultType.Succeeded => Ok(new
            {
                item = result.Item,
                created = result.Created
            }),
            CatalogResultType.StoreNotFound => NotFound(new
            {
                message = "Store not found."
            }),
            CatalogResultType.ProductNotFound => NotFound(new
            {
                message = "The store no longer lists that product."
            }),
            CatalogResultType.NameDoesNotMatchProduct => BadRequest(new
            {
                message = "The name and size must still describe the product you picked. Keep its key words and size; you can drop the brand."
            }),
            _ => BadRequest(new
            {
                message = "Product search is not available for this retailer."
            })
        };
    }
}
