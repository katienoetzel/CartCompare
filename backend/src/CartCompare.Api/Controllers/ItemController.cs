using CartCompare.Api.Models.Requests;
using CartCompare.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IItemService _itemService;

    public ItemsController(IItemService itemService)
    {
        _itemService = itemService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var items = await _itemService.GetActiveAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _itemService.GetByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateItemRequest request)
    {
        var item = await _itemService.CreateAsync(
            request.Name,
            request.Brand,
            request.Size,
            request.Category
        );

        return Ok(item);
    }
}