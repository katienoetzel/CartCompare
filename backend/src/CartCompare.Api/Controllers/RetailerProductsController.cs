using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/retailer-products")]
public class RetailerProductsController : ControllerBase
{
    private readonly IRetailerProductService
        _retailerProductService;

    public RetailerProductsController(
        IRetailerProductService retailerProductService)
    {
        _retailerProductService =
            retailerProductService;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id)
    {
        var product =
            await _retailerProductService
                .GetByIdAsync(
                    id
                );

        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpGet("retailer/{retailerId:int}")]
    public async Task<IActionResult> GetByRetailer(
        int retailerId)
    {
        var products =
            await _retailerProductService
                .GetByRetailerIdAsync(
                    retailerId
                );

        return Ok(products);
    }

    [HttpGet("item/{itemId:int}")]
    public async Task<IActionResult> GetByItem(
        int itemId)
    {
        var products =
            await _retailerProductService
                .GetByItemIdAsync(
                    itemId
                );

        return Ok(products);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateRetailerProductRequest request)
    {
        var result =
            await _retailerProductService
                .CreateAsync(
                    request.ItemId,
                    request.RetailerId,
                    request.ExternalProductId,
                    request.Name,
                    request.Brand,
                    request.Size,
                    request.Upc
                );

        return result.Result switch
        {
            RetailerProductCreateResult
                .Created =>
                Ok(result.RetailerProduct),

            RetailerProductCreateResult
                .AlreadyExists =>
                Ok(result.RetailerProduct),

            RetailerProductCreateResult
                .RetailerNotFound =>
                NotFound(new
                {
                    message =
                        "Retailer not found."
                }),

            RetailerProductCreateResult
                .ItemNotFound =>
                NotFound(new
                {
                    message =
                        "Item not found."
                }),

            _ =>
                StatusCode(500)
        };
    }
}