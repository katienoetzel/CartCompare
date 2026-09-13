using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string password,
        string? defaultPostalCode
    );

    Task<AuthResult> LoginAsync(
        string email,
        string password
    );
}