using CartCompare.Api.Authentication;
using CartCompare.Api.Models.Requests;
using CartCompare.Api.Models.Responses;
using CartCompare.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        IAuthService authService,
        IJwtTokenService jwtTokenService)
    {
        _authService = authService;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            request.DefaultPostalCode
        );

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors
            });
        }

        var user = result.User!;

        var tokenResult =
            _jwtTokenService.CreateToken(user);

        return Ok(new AuthResponse
        {
            Token = tokenResult.Token,
            ExpiresAt = tokenResult.ExpiresAt,
            UserId = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request)
    {
        var result = await _authService.LoginAsync(
            request.Email,
            request.Password
        );

        if (!result.Succeeded || result.User is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var user = result.User;

        var tokenResult =
            _jwtTokenService.CreateToken(user);

        return Ok(new AuthResponse
        {
            Token = tokenResult.Token,
            ExpiresAt = tokenResult.ExpiresAt,
            UserId = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }
}