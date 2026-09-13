using System.Security.Claims;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/memberships")]
[Authorize]
public class MembershipsController : ControllerBase
{
    private readonly IUserRetailerMembershipService
        _membershipService;

    public MembershipsController(
        IUserRetailerMembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMemberships()
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var memberships =
            await _membershipService
                .GetMembershipsAsync(userId.Value);

        return Ok(memberships);
    }

    [HttpPut("{retailerId:int}")]
    public async Task<IActionResult> AddMembership(
        int retailerId)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result =
            await _membershipService.AddMembershipAsync(
                userId.Value,
                retailerId
            );

        return result switch
        {
            MembershipAddResult.Added =>
                Ok(),

            MembershipAddResult.AlreadyExists =>
                Ok(),

            MembershipAddResult.RetailerNotFound =>
                NotFound(new
                {
                    message = "Retailer not found."
                }),

            MembershipAddResult
                .RetailerDoesNotSupportMembership =>
                BadRequest(new
                {
                    message =
                        "This retailer does not support memberships."
                }),

            _ => StatusCode(500)
        };
    }

    [HttpDelete("{retailerId:int}")]
    public async Task<IActionResult> RemoveMembership(
        int retailerId)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var removed =
            await _membershipService.RemoveMembershipAsync(
                userId.Value,
                retailerId
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