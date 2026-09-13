using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public class UserRetailerMembershipService
    : IUserRetailerMembershipService
{
    private readonly IUserRetailerMembershipRepository
        _membershipRepository;

    private readonly IRetailerRepository _retailerRepository;

    public UserRetailerMembershipService(
        IUserRetailerMembershipRepository membershipRepository,
        IRetailerRepository retailerRepository)
    {
        _membershipRepository = membershipRepository;
        _retailerRepository = retailerRepository;
    }

    public async Task<List<UserRetailerMembership>>
        GetMembershipsAsync(int userId)
    {
        return await _membershipRepository.GetByUserIdAsync(userId);
    }

    public async Task<MembershipAddResult> AddMembershipAsync(
        int userId,
        int retailerId)
    {
        var retailer =
            await _retailerRepository.GetByIdAsync(retailerId);

        if (retailer is null)
        {
            return MembershipAddResult.RetailerNotFound;
        }

        if (!retailer.SupportsMembership)
        {
            return MembershipAddResult
                .RetailerDoesNotSupportMembership;
        }

        var existingMembership =
            await _membershipRepository
                .GetByUserAndRetailerAsync(
                    userId,
                    retailerId
                );

        if (existingMembership is not null)
        {
            return MembershipAddResult.AlreadyExists;
        }

        var membership = new UserRetailerMembership
        {
            UserId = userId,
            RetailerId = retailerId,
            CreatedAt = DateTime.UtcNow
        };

        await _membershipRepository.AddAsync(membership);

        return MembershipAddResult.Added;
    }

    public async Task<bool> RemoveMembershipAsync(
        int userId,
        int retailerId)
    {
        var membership =
            await _membershipRepository
                .GetByUserAndRetailerAsync(
                    userId,
                    retailerId
                );

        if (membership is null)
        {
            return false;
        }

        await _membershipRepository.RemoveAsync(membership);

        return true;
    }
}