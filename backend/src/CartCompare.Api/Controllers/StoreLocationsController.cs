using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/store-locations")]
public class StoreLocationsController : ControllerBase
{
    private readonly IStoreLocationService _storeLocationService;

    public StoreLocationsController(
        IStoreLocationService storeLocationService)
    {
        _storeLocationService = storeLocationService;
    }

    [HttpGet("retailer/{retailerId:int}")]
    public async Task<IActionResult> GetByRetailer(
        int retailerId)
    {
        var stores =
            await _storeLocationService
                .GetByRetailerIdAsync(retailerId);

        return Ok(stores);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var store =
            await _storeLocationService.GetByIdAsync(id);

        if (store is null)
        {
            return NotFound();
        }

        return Ok(store);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateStoreLocationRequest request)
    {
        var result =
            await _storeLocationService.CreateAsync(
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
            StoreLocationCreateResult.Created =>
                Ok(result.StoreLocation),

            StoreLocationCreateResult.AlreadyExists =>
                Ok(result.StoreLocation),

            StoreLocationCreateResult.RetailerNotFound =>
                NotFound(new
                {
                    message = "Retailer not found."
                }),

            _ => StatusCode(500)
        };
    }
}