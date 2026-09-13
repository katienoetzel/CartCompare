using CartCompare.Entities;

namespace CartCompare.Api.Authentication;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(
        ApplicationUser user
    );
}