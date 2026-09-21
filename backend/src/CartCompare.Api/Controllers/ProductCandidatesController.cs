using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/product-candidates")]
[Authorize(Policy = "AdminOnly")]
public class ProductCandidatesController
    : ControllerBase
{
    private readonly IProductCandidateService
        _productCandidateService;

    public ProductCandidatesController(
        IProductCandidateService productCandidateService)
    {
        _productCandidateService =
            productCandidateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCandidates(
        [FromQuery] int itemId,
        [FromQuery] int storeLocationId,
        [FromQuery] string? query = null)
    {
        if (
            itemId <= 0 ||
            storeLocationId <= 0
        )
        {
            return BadRequest(new
            {
                message =
                    "Item ID and store location ID must be greater than zero."
            });
        }

        var result =
            await _productCandidateService
                .FindCandidatesAsync(
                    itemId,
                    storeLocationId,
                    query
                );

        return result.Result switch
        {
            ProductCandidateSearchResultType
                .Succeeded =>
                Ok(result),

            ProductCandidateSearchResultType
                .ItemNotFound =>
                NotFound(new
                {
                    message =
                        "Item not found."
                }),

            ProductCandidateSearchResultType
                .StoreLocationNotFound =>
                NotFound(new
                {
                    message =
                        "Store location not found."
                }),

            ProductCandidateSearchResultType
                .RetailerNotFound =>
                NotFound(new
                {
                    message =
                        "Retailer not found."
                }),

            ProductCandidateSearchResultType
                .ProviderNotFound =>
                BadRequest(new
                {
                    message =
                        "No price provider supports this retailer."
                }),

            _ =>
                StatusCode(500)
        };
    }
}