using System.Security.Claims;

using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/prices")]
public class PricesController : ControllerBase
{
    private readonly IPriceService
        _priceService;

    public PricesController(
        IPriceService priceService)
    {
        _priceService =
            priceService;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id)
    {
        var price =
            await _priceService
                .GetByIdAsync(
                    id
                );

        if (price is null)
        {
            return NotFound();
        }

        return Ok(price);
    }

    [HttpGet(
        "product/{retailerProductId:int}/store/{storeLocationId:int}"
    )]
    public async Task<IActionResult>
        GetByProductAndStore(
            int retailerProductId,
            int storeLocationId)
    {
        var price =
            await _priceService
                .GetByProductAndStoreAsync(
                    retailerProductId,
                    storeLocationId
                );

        if (price is null)
        {
            return NotFound();
        }

        return Ok(price);
    }

    [HttpGet("product/{retailerProductId:int}")]
    public async Task<IActionResult> GetByProduct(
        int retailerProductId)
    {
        var prices =
            await _priceService
                .GetByRetailerProductIdAsync(
                    retailerProductId
                );

        return Ok(prices);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    public async Task<IActionResult> Upsert(
        UpsertPriceRequest request)
    {
        var result =
            await _priceService
                .UpsertAsync(
                    request.RetailerProductId,
                    request.StoreLocationId,
                    request.RegularPrice,
                    request.SalePrice,
                    request.MemberPrice,
                    request.AvailabilityStatus,
                    request.SourceProvider,
                    request.SourceUpdatedAt
                );

        return result.Result switch
        {
            PriceUpsertResultType
                .Created =>
                Ok(result.StoredPrice),

            PriceUpsertResultType
                .Updated =>
                Ok(result.StoredPrice),

            PriceUpsertResultType
                .RetailerProductNotFound =>
                NotFound(new
                {
                    message =
                        "Retailer product not found."
                }),

            PriceUpsertResultType
                .StoreLocationNotFound =>
                NotFound(new
                {
                    message =
                        "Store location not found."
                }),

            PriceUpsertResultType
                .RetailerMismatch =>
                BadRequest(new
                {
                    message =
                        "The retailer product and store location belong to different retailers."
                }),

            PriceUpsertResultType
                .InvalidPrice =>
                BadRequest(new
                {
                    message =
                        "Price values cannot be negative."
                }),

            _ =>
                StatusCode(500)
        };
    }

    [Authorize]
    [HttpGet(
        "product/{retailerProductId:int}/store/{storeLocationId:int}/effective"
    )]
    public async Task<IActionResult> GetEffectivePrice(
        int retailerProductId,
        int storeLocationId)
    {
        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;

        if (
            !int.TryParse(
                userIdClaim,
                out var userId
            )
        )
        {
            return Unauthorized();
        }

        var result =
            await _priceService
                .GetEffectivePriceAsync(
                    userId,
                    retailerProductId,
                    storeLocationId
                );

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}