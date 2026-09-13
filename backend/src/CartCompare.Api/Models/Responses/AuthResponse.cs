namespace CartCompare.Api.Models.Responses;

public class AuthResponse
{
    public required string Token { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int UserId { get; set; }

    public required string Email { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }
}