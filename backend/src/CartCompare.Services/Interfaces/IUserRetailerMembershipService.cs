using CartCompare.Entities;
using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IUserRetailerMembershipService
{
    Task<List<UserRetailerMembership>> GetMembershipsAsync(
        int userId
    );

    Task<MembershipAddResult> AddMembershipAsync(
        int userId,
        int retailerId
    );

    Task<bool> RemoveMembershipAsync(
        int userId,
        int retailerId
    );
}