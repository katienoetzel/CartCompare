using System.Security.Claims;
using CartCompare.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SavedItemsController : ControllerBase
{
    private readonly ISavedItemService _savedItemService;

    public SavedItemsController(ISavedItemService savedItemService)
    {
        _savedItemService = savedItemService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSavedItems()
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var savedItems =
            await _savedItemService.GetSavedItemsAsync(userId.Value);

        return Ok(savedItems);
    }

    [HttpPost("{itemId:int}")]
    public async Task<IActionResult> SaveItem(int itemId)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var savedItem =
            await _savedItemService.SaveItemAsync(
                userId.Value,
                itemId
            );

        return Ok(savedItem);
    }

    [HttpDelete("{itemId:int}")]
    public async Task<IActionResult> RemoveSavedItem(int itemId)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var removed =
            await _savedItemService.RemoveSavedItemAsync(
                userId.Value,
                itemId
            );

        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    private int? GetUserId()
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}