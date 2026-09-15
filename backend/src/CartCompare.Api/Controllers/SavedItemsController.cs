using System.Security.Claims;
using CartCompare.Api.Models.Responses;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
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
            await _savedItemService.GetSavedItemsAsync(
                userId.Value
            );

        var response =
            savedItems
                .Select(ToResponse)
                .ToList();

        return Ok(response);
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

        if (savedItem is null)
        {
            return NotFound(new
            {
                message = "Item not found."
            });
        }

        return Ok(ToResponse(savedItem));
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
    private static SavedItemResponse ToResponse(
    SavedItemDetails savedItem)
    {
        return new SavedItemResponse
        {
            Id = savedItem.SavedItemId,
            ItemId = savedItem.ItemId,
            Name = savedItem.ItemName,
            Brand = savedItem.Brand,
            Size = savedItem.Size,
            Category = savedItem.Category,
            CreatedAt = savedItem.CreatedAt
        };
    }
}