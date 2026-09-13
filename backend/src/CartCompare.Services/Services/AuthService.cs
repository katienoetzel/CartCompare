using CartCompare.Entities;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using Microsoft.AspNetCore.Identity;

namespace CartCompare.Services.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<AuthResult> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string password,
        string? defaultPostalCode)
    {
        var normalizedEmail = email.Trim();

        var existingUser =
            await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string>
                {
                    "An account with this email already exists."
                }
            };
        }

        var user = new ApplicationUser
        {
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = normalizedEmail,
            UserName = normalizedEmail,
            DefaultPostalCode = defaultPostalCode?.Trim()
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = result.Errors
                    .Select(error => error.Description)
                    .ToList()
            };
        }

        return new AuthResult
        {
            Succeeded = true,
            User = user
        };
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());

        if (user is null)
        {
            return new AuthResult
            {
                Succeeded = false
            };
        }

        var passwordIsValid =
            await _userManager.CheckPasswordAsync(user, password);

        if (!passwordIsValid)
        {
            return new AuthResult
            {
                Succeeded = false
            };
        }

        return new AuthResult
        {
            Succeeded = true,
            User = user
        };
    }
}