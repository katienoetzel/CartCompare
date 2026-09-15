using System.Security.Claims;
using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/comparisons")]
[Authorize]
public class ComparisonsController : ControllerBase
{
    private readonly IStoreComparisonService
        _storeComparisonService;

    public ComparisonsController(
        IStoreComparisonService storeComparisonService)
    {
        _storeComparisonService = storeComparisonService;
    }

    [HttpGet("stores/{storeLocationId:int}")]
    public async Task<IActionResult> CompareStore(
        int storeLocationId)
    {
        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result =
            await _storeComparisonService.CompareStoreAsync(
                userId,
                storeLocationId
            );

        if (result is null)
        {
            return NotFound(new
            {
                message = "Store location not found."
            });
        }

        return Ok(result);
    }
    [HttpPost("stores")]
    public async Task<IActionResult> CompareStores(
    CompareStoresRequest request)
    {
        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (
            request.StoreLocationIds.Any(
                storeId => storeId <= 0
            ))
        {
            return BadRequest(new
            {
                message =
                    "Store location IDs must be positive."
            });
        }

        var result =
            await _storeComparisonService
                .CompareStoresAsync(
                    userId,
                    request.StoreLocationIds
                );

        return Ok(result);
    }
}