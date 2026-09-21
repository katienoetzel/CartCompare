using CartCompare.Infrastructure.Providers.Kroger;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize(Policy = "AdminOnly")]
public class IntegrationsController : ControllerBase
{
    private readonly KrogerTokenService
        _krogerTokenService;

    private readonly KrogerPriceProvider
        _krogerPriceProvider;

    public IntegrationsController(
        KrogerTokenService krogerTokenService,
        KrogerPriceProvider krogerPriceProvider)
    {
        _krogerTokenService =
            krogerTokenService;

        _krogerPriceProvider =
            krogerPriceProvider;
    }

    [HttpGet("kroger/status")]
    public async Task<IActionResult> GetKrogerStatus()
    {
        await _krogerTokenService
            .GetAccessTokenAsync();

        return Ok(new
        {
            connected = true,
            provider = "Kroger"
        });
    }

    [HttpGet("kroger/stores")]
    public async Task<IActionResult> GetKrogerStores(
        [FromQuery] string? postalCode)
    {
        if (
            string.IsNullOrWhiteSpace(
                postalCode
            )
        )
        {
            return BadRequest(new
            {
                message =
                    "Postal code is required."
            });
        }

        var stores =
            await _krogerPriceProvider
                .FindStoresAsync(
                    "Kroger",
                    postalCode
                );

        return Ok(stores);
    }

    [HttpGet("kroger/products")]
    public async Task<IActionResult> SearchKrogerProducts(
        [FromQuery] string? locationId,
        [FromQuery] string? query)
    {
        if (
            string.IsNullOrWhiteSpace(
                locationId
            )
            ||
            string.IsNullOrWhiteSpace(
                query
            )
        )
        {
            return BadRequest(new
            {
                message =
                    "Location ID and search query are required."
            });
        }

        var products =
            await _krogerPriceProvider
                .SearchProductsAsync(
                    "Kroger",
                    locationId,
                    query
                );

        return Ok(products);
    }

    [HttpGet("kroger/price")]
    public async Task<IActionResult> GetKrogerPrice(
        [FromQuery] string? locationId,
        [FromQuery] string? productId)
    {
        if (
            string.IsNullOrWhiteSpace(
                locationId
            )
            ||
            string.IsNullOrWhiteSpace(
                productId
            )
        )
        {
            return BadRequest(new
            {
                message =
                    "Location ID and product ID are required."
            });
        }

        var price =
            await _krogerPriceProvider
                .GetPriceAsync(
                    "Kroger",
                    locationId,
                    productId
                );

        if (price is null)
        {
            return NotFound();
        }

        return Ok(price);
    }
}