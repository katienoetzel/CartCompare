using CartCompare.Infrastructure.Providers.Kroger;
using CartCompare.Infrastructure.Providers.LowesFoods;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize]
public class IntegrationsController
    : ControllerBase
{
    private readonly KrogerTokenService
        _krogerTokenService;

    private readonly KrogerPriceProvider
        _krogerPriceProvider;

    private readonly LowesFoodsPriceProvider
        _lowesFoodsPriceProvider;

    private readonly IWebHostEnvironment
        _environment;

    public IntegrationsController(
        KrogerTokenService krogerTokenService,
        KrogerPriceProvider krogerPriceProvider,
        LowesFoodsPriceProvider lowesFoodsPriceProvider,
        IWebHostEnvironment environment)
    {
        _krogerTokenService =
            krogerTokenService;

        _krogerPriceProvider =
            krogerPriceProvider;

        _lowesFoodsPriceProvider =
            lowesFoodsPriceProvider;

        _environment =
            environment;
    }

    // =================================================
    // Kroger
    // =================================================

    [HttpGet("kroger/status")]
    public async Task<IActionResult>
        GetKrogerStatus()
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
    public async Task<IActionResult>
        GetKrogerStores(
            [FromQuery] string postalCode)
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
    public async Task<IActionResult>
        SearchKrogerProducts(
            [FromQuery] string locationId,
            [FromQuery] string query)
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
    public async Task<IActionResult>
        GetKrogerPrice(
            [FromQuery] string locationId,
            [FromQuery] string productId)
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

    // =================================================
    // Lowes Foods
    // =================================================

    [HttpGet("lowes-foods/stores")]
    public async Task<IActionResult>
        GetLowesFoodsStores(
            [FromQuery] string postalCode)
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
            await _lowesFoodsPriceProvider
                .FindStoresAsync(
                    "Lowes Foods",
                    postalCode
                );

        return Ok(stores);
    }

    [HttpGet("lowes-foods/products")]
    public async Task<IActionResult>
        SearchLowesFoodsProducts(
            [FromQuery] string locationId,
            [FromQuery] string query)
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
            await _lowesFoodsPriceProvider
                .SearchProductsAsync(
                    "Lowes Foods",
                    locationId,
                    query
                );

        return Ok(products);
    }

    [HttpGet("lowes-foods/price")]
    public async Task<IActionResult>
        GetLowesFoodsPrice(
            [FromQuery] string locationId,
            [FromQuery] string productId)
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
            await _lowesFoodsPriceProvider
                .GetPriceAsync(
                    "Lowes Foods",
                    locationId,
                    productId
                );

        if (price is null)
        {
            return NotFound();
        }

        return Ok(price);
    }

    // =================================================
    // Development-only Lowes Foods smoke test
    // =================================================
    //
    // This intentionally bypasses JWT only in the
    // Development environment so the provider can be
    // verified quickly from PowerShell.
    //
    // Production returns 404.

    [AllowAnonymous]
    [HttpGet("lowes-foods/dev-search")]
    public async Task<IActionResult>
        DevSearchLowesFoods(
            [FromQuery] string locationId,
            [FromQuery] string query)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

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
            await _lowesFoodsPriceProvider
                .SearchProductsAsync(
                    "Lowes Foods",
                    locationId,
                    query
                );

        return Ok(products);
    }
}
