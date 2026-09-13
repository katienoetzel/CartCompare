using CartCompare.Api.Models.Requests;
using CartCompare.Entities;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Services;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RetailersController : ControllerBase
{
    private readonly IRetailerService _retailerService;

    public RetailersController(IRetailerService retailerService)
    {
        _retailerService = retailerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var retailers = await _retailerService.GetActiveAsync();

        return Ok(retailers);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRetailerRequest request)
    {
        var retailer = await _retailerService.CreateAsync(
            request.Name,
            request.SupportsMembership
        );

        return Ok(retailer);
    }
}