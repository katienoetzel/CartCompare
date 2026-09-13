using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.Repositories.Repositories;

public class UserRetailerMembershipRepository
    : IUserRetailerMembershipRepository
{
    private readonly CartCompareDbContext _dbContext;

    public UserRetailerMembershipRepository(
        CartCompareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<UserRetailerMembership>> GetByUserIdAsync(
        int userId)
    {
        return await _dbContext.UserRetailerMemberships
            .Where(m => m.UserId == userId)
            .ToListAsync();
    }

    public async Task<UserRetailerMembership?>
        GetByUserAndRetailerAsync(
            int userId,
            int retailerId)
    {
        return await _dbContext.UserRetailerMemberships
            .FirstOrDefaultAsync(m =>
                m.UserId == userId &&
                m.RetailerId == retailerId
            );
    }

    public async Task AddAsync(
        UserRetailerMembership membership)
    {
        await _dbContext.UserRetailerMemberships
            .AddAsync(membership);

        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveAsync(
        UserRetailerMembership membership)
    {
        _dbContext.UserRetailerMemberships.Remove(membership);

        await _dbContext.SaveChangesAsync();
    }
}