using System.Security.Claims;
using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CartCompare.Api.Controllers;

// Shoppers add missing grocery items by picking a real product from a
// nearby store's catalog. The server builds the item from that product;
// shoppers only choose between its options and never type a name.
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

    // Searches every retailer; no location is needed (see ICatalogService).
    [HttpGet("products")]
    public async Task<IActionResult> SearchProducts(
        [FromQuery] string? query)
    {
        var trimmed = query?.Trim() ?? "";

        if (trimmed.Length is < 2 or > 100)
        {
            return BadRequest(new
            {
                message = "Enter 2 to 100 characters to search."
            });
        }

        var result =
            await _catalogService.SearchAsync(
                trimmed
            );

        return Ok(new
        {
            retailers = result.Retailers,
            failedRetailers = result.FailedRetailers
        });
    }

    // What a picked product could become: catalog items it already
    // counts as, and the new items that could be created from it.
    [HttpGet("products/{externalProductId}/options")]
    public async Task<IActionResult> GetOptions(
        string externalProductId,
        [FromQuery] int storeLocationId)
    {
        if (storeLocationId <= 0 || externalProductId.Length > 100)
        {
            return BadRequest(new
            {
                message = "Choose a product from a store search."
            });
        }

        var result =
            await _catalogService.GetOptionsAsync(
                storeLocationId,
                externalProductId
            );

        return result.Result == CatalogResultType.Succeeded
            ? Ok(new
            {
                product = result.Product,
                existingItems = result.ExistingItems,
                newItems = result.NewItems
            })
            : Failure(result.Result);
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem(
        CreateCatalogItemRequest request)
    {
        if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized();
        }

        var result =
            await _catalogService.CreateItemAsync(
                request.StoreLocationId,
                request.ExternalProductId,
                request.Kind,
                userId
            );

        return result.Result == CatalogResultType.Succeeded
            ? Ok(new
            {
                item = result.Item,
                created = result.Created
            })
            : Failure(result.Result);
    }

    private IActionResult Failure(
        CatalogResultType result)
    {
        return result switch
        {
            CatalogResultType.StoreNotFound => NotFound(new
            {
                message = "Store not found."
            }),
            CatalogResultType.ProductNotFound => NotFound(new
            {
                message = "The store no longer lists that product, or it has no size."
            }),
            CatalogResultType.OptionNotAvailable => BadRequest(new
            {
                message = "That kind of item can't be made from this product."
            }),
            _ => BadRequest(new
            {
                message = "Product search is not available for this retailer."
            })
        };
    }
}
