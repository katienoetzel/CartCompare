using System.Security.Claims;
using CartCompare.Api.Models.Requests;
using CartCompare.Api.Models.Responses;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/grocery-list")]
[Authorize]
public class GroceryListController : ControllerBase
{
    private readonly IGroceryListService _groceryListService;

    public GroceryListController(
        IGroceryListService groceryListService)
    {
        _groceryListService = groceryListService;
    }

    [HttpGet]
    public async Task<IActionResult> GetItems()
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var items =
            await _groceryListService.GetItemsAsync(
                userId.Value
            );

        var response =
            items
                .Select(ToResponse)
                .ToList();

        return Ok(response);
    }

    [HttpPut("{itemId:int}")]
    public async Task<IActionResult> SetQuantity(
        int itemId,
        SetGroceryListQuantityRequest request)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var groceryListItem =
            await _groceryListService.SetQuantityAsync(
                userId.Value,
                itemId,
                request.Quantity
            );

        if (groceryListItem is null)
        {
            return NotFound(new
            {
                message = "Item not found."
            });
        }

        return Ok(ToResponse(groceryListItem));
    }

    [HttpDelete("{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int itemId)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var removed =
            await _groceryListService.RemoveItemAsync(
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

    private static GroceryListItemResponse ToResponse(
    GroceryListItemDetails item)
    {
        return new GroceryListItemResponse
        {
            Id = item.GroceryListItemId,
            ItemId = item.ItemId,
            Name = item.ItemName,
            Brand = item.Brand,
            Size = item.Size,
            Category = item.Category,
            Quantity = item.Quantity,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}