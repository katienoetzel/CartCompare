using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Repositories;

public class UserRetailerMembershipRepositoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_PersistsMembership()
    {
        // Arrange
        var seeded =
            await SeedUserAndRetailerAsync();

        var membership =
            CreateMembership(
                seeded.UserId,
                seeded.RetailerId
            );

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new UserRetailerMembershipRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                membership
            );
        }

        // Assert with a fresh context.
        await using var readContext =
            CreateDbContext();

        var readRepository =
            new UserRetailerMembershipRepository(
                readContext
            );

        var stored =
            await readRepository
                .GetByUserAndRetailerAsync(
                    seeded.UserId,
                    seeded.RetailerId
                );

        Assert.NotNull(stored);

        Assert.True(
            stored.Id > 0
        );

        Assert.Equal(
            seeded.UserId,
            stored.UserId
        );

        Assert.Equal(
            seeded.RetailerId,
            stored.RetailerId
        );
    }

    [Fact]
    public async Task GetByUserAndRetailerAsync_WhenMembershipDoesNotExist_ReturnsNull()
    {
        // Arrange
        var seeded =
            await SeedUserAndRetailerAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new UserRetailerMembershipRepository(
                context
            );

        // Act
        var result =
            await repository
                .GetByUserAndRetailerAsync(
                    seeded.UserId,
                    seeded.RetailerId
                );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyRequestedUsersMemberships()
    {
        // Arrange
        var data =
            await SeedTwoUsersAndTwoRetailersAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new UserRetailerMembershipRepository(
                    writeContext
                );

            await writeRepository.AddAsync(
                CreateMembership(
                    data.FirstUserId,
                    data.FirstRetailerId
                )
            );

            await writeRepository.AddAsync(
                CreateMembership(
                    data.FirstUserId,
                    data.SecondRetailerId
                )
            );

            await writeRepository.AddAsync(
                CreateMembership(
                    data.SecondUserId,
                    data.FirstRetailerId
                )
            );
        }

        await using var readContext =
            CreateDbContext();

        var readRepository =
            new UserRetailerMembershipRepository(
                readContext
            );

        // Act
        var result =
            await readRepository.GetByUserIdAsync(
                data.FirstUserId
            );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.All(
            result,
            membership =>
                Assert.Equal(
                    data.FirstUserId,
                    membership.UserId
                )
        );

        Assert.Contains(
            result,
            membership =>
                membership.RetailerId ==
                data.FirstRetailerId
        );

        Assert.Contains(
            result,
            membership =>
                membership.RetailerId ==
                data.SecondRetailerId
        );
    }

    [Fact]
    public async Task AddAsync_WhenSameUserAndRetailerAreAddedTwice_DatabaseRejectsDuplicate()
    {
        // Arrange
        var seeded =
            await SeedUserAndRetailerAsync();

        await using var context =
            CreateDbContext();

        var repository =
            new UserRetailerMembershipRepository(
                context
            );

        await repository.AddAsync(
            CreateMembership(
                seeded.UserId,
                seeded.RetailerId
            )
        );

        var duplicate =
            CreateMembership(
                seeded.UserId,
                seeded.RetailerId
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    duplicate
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenDifferentUsersUseSameRetailer_AllowsBothRows()
    {
        // Arrange
        var data =
            await SeedTwoUsersAndOneRetailerAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new UserRetailerMembershipRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                CreateMembership(
                    data.FirstUserId,
                    data.RetailerId
                )
            );

            await writeRepository.AddAsync(
                CreateMembership(
                    data.SecondUserId,
                    data.RetailerId
                )
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var rows =
            await readContext
                .Set<UserRetailerMembership>()
                .Where(
                    membership =>
                        membership.RetailerId ==
                        data.RetailerId
                )
                .ToListAsync();

        Assert.Equal(
            2,
            rows.Count
        );

        Assert.Contains(
            rows,
            membership =>
                membership.UserId ==
                data.FirstUserId
        );

        Assert.Contains(
            rows,
            membership =>
                membership.UserId ==
                data.SecondUserId
        );
    }

    [Fact]
    public async Task AddAsync_WhenSameUserUsesDifferentRetailers_AllowsBothRows()
    {
        // Arrange
        var data =
            await SeedOneUserAndTwoRetailersAsync();

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new UserRetailerMembershipRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                CreateMembership(
                    data.UserId,
                    data.FirstRetailerId
                )
            );

            await writeRepository.AddAsync(
                CreateMembership(
                    data.UserId,
                    data.SecondRetailerId
                )
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var rows =
            await readContext
                .Set<UserRetailerMembership>()
                .Where(
                    membership =>
                        membership.UserId ==
                        data.UserId
                )
                .ToListAsync();

        Assert.Equal(
            2,
            rows.Count
        );

        Assert.Contains(
            rows,
            membership =>
                membership.RetailerId ==
                data.FirstRetailerId
        );

        Assert.Contains(
            rows,
            membership =>
                membership.RetailerId ==
                data.SecondRetailerId
        );
    }

    [Fact]
    public async Task RemoveAsync_RemovesMembershipFromDatabase()
    {
        // Arrange
        var seeded =
            await SeedUserAndRetailerAsync();

        int membershipId;

        await using (
            var context =
                CreateDbContext())
        {
            var repository =
                new UserRetailerMembershipRepository(
                    context
                );

            var membership =
                CreateMembership(
                    seeded.UserId,
                    seeded.RetailerId
                );

            await repository.AddAsync(
                membership
            );

            membershipId =
                membership.Id;

            // Act
            await repository.RemoveAsync(
                membership
            );
        }

        // Assert
        await using var readContext =
            CreateDbContext();

        var stillExists =
            await readContext
                .Set<UserRetailerMembership>()
                .AnyAsync(
                    membership =>
                        membership.Id ==
                        membershipId
                );

        Assert.False(
            stillExists
        );
    }

    [Fact]
    public async Task AddAsync_WhenUserDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                "Kroger"
            );

        await using var context =
            CreateDbContext();

        var repository =
            new UserRetailerMembershipRepository(
                context
            );

        var membership =
            CreateMembership(
                userId: 999999,
                retailerId: retailerId
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    membership
                )
        );
    }

    [Fact]
    public async Task AddAsync_WhenRetailerDoesNotExist_DatabaseRejectsForeignKey()
    {
        // Arrange
        var userId =
            await SeedUserAsync(
                "missing-retailer-user@example.com"
            );

        await using var context =
            CreateDbContext();

        var repository =
            new UserRetailerMembershipRepository(
                context
            );

        var membership =
            CreateMembership(
                userId,
                retailerId: 999999
            );

        // Act + Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            async () =>
                await repository.AddAsync(
                    membership
                )
        );
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    private static UserRetailerMembership
        CreateMembership(
            int userId,
            int retailerId)
    {
        return new UserRetailerMembership
        {
            UserId =
                userId,

            RetailerId =
                retailerId,

            CreatedAt =
                DateTime.UtcNow
        };
    }

    private async Task<(
        int UserId,
        int RetailerId)>
        SeedUserAndRetailerAsync()
    {
        await using var context =
            CreateDbContext();

        var user =
            CreateUser(
                "membership-user@example.com"
            );

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        context.Set<ApplicationUser>()
            .Add(user);

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        return (
            user.Id,
            retailer.Id
        );
    }

    private async Task<int>
        SeedUserAsync(
            string email)
    {
        await using var context =
            CreateDbContext();

        var user =
            CreateUser(
                email
            );

        context.Set<ApplicationUser>()
            .Add(user);

        await context.SaveChangesAsync();

        return user.Id;
    }

    private async Task<int>
        SeedRetailerAsync(
            string name)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            CreateRetailer(
                name
            );

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        return retailer.Id;
    }

    private async Task<(
        int FirstUserId,
        int SecondUserId,
        int RetailerId)>
        SeedTwoUsersAndOneRetailerAsync()
    {
        await using var context =
            CreateDbContext();

        var firstUser =
            CreateUser(
                "first-membership-user@example.com"
            );

        var secondUser =
            CreateUser(
                "second-membership-user@example.com"
            );

        var retailer =
            CreateRetailer(
                "Kroger"
            );

        context.Set<ApplicationUser>()
            .AddRange(
                firstUser,
                secondUser
            );

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        return (
            firstUser.Id,
            secondUser.Id,
            retailer.Id
        );
    }

    private async Task<(
        int UserId,
        int FirstRetailerId,
        int SecondRetailerId)>
        SeedOneUserAndTwoRetailersAsync()
    {
        await using var context =
            CreateDbContext();

        var user =
            CreateUser(
                "membership-user@example.com"
            );

        var firstRetailer =
            CreateRetailer(
                "Kroger"
            );

        var secondRetailer =
            CreateRetailer(
                "Harris Teeter"
            );

        context.Set<ApplicationUser>()
            .Add(user);

        context.Set<Retailer>()
            .AddRange(
                firstRetailer,
                secondRetailer
            );

        await context.SaveChangesAsync();

        return (
            user.Id,
            firstRetailer.Id,
            secondRetailer.Id
        );
    }

    private async Task<(
        int FirstUserId,
        int SecondUserId,
        int FirstRetailerId,
        int SecondRetailerId)>
        SeedTwoUsersAndTwoRetailersAsync()
    {
        await using var context =
            CreateDbContext();

        var firstUser =
            CreateUser(
                "first-membership-user@example.com"
            );

        var secondUser =
            CreateUser(
                "second-membership-user@example.com"
            );

        var firstRetailer =
            CreateRetailer(
                "Kroger"
            );

        var secondRetailer =
            CreateRetailer(
                "Harris Teeter"
            );

        context.Set<ApplicationUser>()
            .AddRange(
                firstUser,
                secondUser
            );

        context.Set<Retailer>()
            .AddRange(
                firstRetailer,
                secondRetailer
            );

        await context.SaveChangesAsync();

        return (
            firstUser.Id,
            secondUser.Id,
            firstRetailer.Id,
            secondRetailer.Id
        );
    }

    private static ApplicationUser
        CreateUser(
            string email)
    {
        return new ApplicationUser
        {
            UserName =
                email,

            NormalizedUserName =
                email.ToUpperInvariant(),

            Email =
                email,

            NormalizedEmail =
                email.ToUpperInvariant(),

            EmailConfirmed =
                true
        };
    }

    private static Retailer
        CreateRetailer(
            string name)
    {
        return new Retailer
        {
            Name =
                name,

            SupportsMembership =
                true,

            IsActive =
                true,

            CreatedAt =
                DateTime.UtcNow
        };
    }
}