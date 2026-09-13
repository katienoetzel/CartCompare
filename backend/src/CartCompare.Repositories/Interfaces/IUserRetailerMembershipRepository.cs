using CartCompare.Entities;

namespace CartCompare.Repositories.Interfaces;

public interface IUserRetailerMembershipRepository
{
    Task<List<UserRetailerMembership>> GetByUserIdAsync(int userId);

    Task<UserRetailerMembership?> GetByUserAndRetailerAsync(
        int userId,
        int retailerId
    );

    Task AddAsync(UserRetailerMembership membership);

    Task RemoveAsync(UserRetailerMembership membership);
}