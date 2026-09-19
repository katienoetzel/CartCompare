using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class UserRetailerMembershipServiceTests
{
    private readonly Mock<IUserRetailerMembershipRepository>
        _membershipRepositoryMock;

    private readonly Mock<IRetailerRepository>
        _retailerRepositoryMock;

    private readonly UserRetailerMembershipService
        _service;

    public UserRetailerMembershipServiceTests()
    {
        _membershipRepositoryMock =
            new Mock<IUserRetailerMembershipRepository>();

        _retailerRepositoryMock =
            new Mock<IRetailerRepository>();

        _service =
            new UserRetailerMembershipService(
                _membershipRepositoryMock.Object,
                _retailerRepositoryMock.Object
            );
    }

    [Fact]
    public async Task GetMembershipsAsync_ReturnsUsersMemberships()
    {
        // Arrange
        var userId = 20;

        var memberships =
            new List<UserRetailerMembership>
            {
                CreateMembership(
                    id: 1,
                    userId: userId,
                    retailerId: 1
                ),

                CreateMembership(
                    id: 2,
                    userId: userId,
                    retailerId: 2
                )
            };

        _membershipRepositoryMock
            .Setup(repository =>
                repository.GetByUserIdAsync(
                    userId
                )
            )
            .ReturnsAsync(memberships);

        // Act
        var result =
            await _service.GetMembershipsAsync(
                userId
            );

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.Contains(
            result,
            membership =>
                membership.RetailerId == 1
        );

        Assert.Contains(
            result,
            membership =>
                membership.RetailerId == 2
        );

        _membershipRepositoryMock.Verify(
            repository =>
                repository.GetByUserIdAsync(
                    userId
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetMembershipsAsync_WhenUserHasNone_ReturnsEmptyList()
    {
        // Arrange
        _membershipRepositoryMock
            .Setup(repository =>
                repository.GetByUserIdAsync(
                    20
                )
            )
            .ReturnsAsync(
                new List<UserRetailerMembership>()
            );

        // Act
        var result =
            await _service.GetMembershipsAsync(
                20
            );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task AddMembershipAsync_WhenRetailerDoesNotExist_ReturnsRetailerNotFound()
    {
        // Arrange
        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    999
                )
            )
            .ReturnsAsync(
                (Retailer?)null
            );

        // Act
        var result =
            await _service.AddMembershipAsync(
                userId: 20,
                retailerId: 999
            );

        // Assert
        Assert.Equal(
            MembershipAddResult.RetailerNotFound,
            result
        );

        _membershipRepositoryMock.Verify(
            repository =>
                repository.GetByUserAndRetailerAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>()
                ),
            Times.Never
        );

        _membershipRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<UserRetailerMembership>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task AddMembershipAsync_WhenRetailerDoesNotSupportMembership_ReturnsRetailerDoesNotSupportMembership()
    {
        // Arrange
        var retailer =
            CreateRetailer(
                id: 1,
                supportsMembership: false
            );

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    retailer.Id
                )
            )
            .ReturnsAsync(retailer);

        // Act
        var result =
            await _service.AddMembershipAsync(
                userId: 20,
                retailerId: retailer.Id
            );

        // Assert
        Assert.Equal(
            MembershipAddResult
                .RetailerDoesNotSupportMembership,
            result
        );

        _membershipRepositoryMock.Verify(
            repository =>
                repository.GetByUserAndRetailerAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>()
                ),
            Times.Never
        );

        _membershipRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<UserRetailerMembership>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task AddMembershipAsync_WhenMembershipAlreadyExists_ReturnsAlreadyExistsWithoutAddingDuplicate()
    {
        // Arrange
        var userId = 20;

        var retailer =
            CreateRetailer(
                id: 1,
                supportsMembership: true
            );

        var existing =
            CreateMembership(
                id: 100,
                userId: userId,
                retailerId: retailer.Id
            );

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    retailer.Id
                )
            )
            .ReturnsAsync(retailer);

        _membershipRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndRetailerAsync(
                    userId,
                    retailer.Id
                )
            )
            .ReturnsAsync(existing);

        // Act
        var result =
            await _service.AddMembershipAsync(
                userId,
                retailer.Id
            );

        // Assert
        Assert.Equal(
            MembershipAddResult.AlreadyExists,
            result
        );

        _membershipRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<UserRetailerMembership>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task AddMembershipAsync_WhenValid_CreatesMembershipAndReturnsAdded()
    {
        // Arrange
        var userId = 20;

        var retailer =
            CreateRetailer(
                id: 1,
                supportsMembership: true
            );

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    retailer.Id
                )
            )
            .ReturnsAsync(retailer);

        _membershipRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndRetailerAsync(
                    userId,
                    retailer.Id
                )
            )
            .ReturnsAsync(
                (UserRetailerMembership?)null
            );

        UserRetailerMembership? addedMembership =
            null;

        _membershipRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<UserRetailerMembership>()
                )
            )
            .Callback<UserRetailerMembership>(
                membership =>
                {
                    membership.Id = 100;
                    addedMembership = membership;
                }
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.AddMembershipAsync(
                userId,
                retailer.Id
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.Equal(
            MembershipAddResult.Added,
            result
        );

        Assert.NotNull(
            addedMembership
        );

        Assert.Equal(
            userId,
            addedMembership.UserId
        );

        Assert.Equal(
            retailer.Id,
            addedMembership.RetailerId
        );

        Assert.InRange(
            addedMembership.CreatedAt,
            before,
            after
        );

        _membershipRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<UserRetailerMembership>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RemoveMembershipAsync_WhenMembershipDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _membershipRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndRetailerAsync(
                    20,
                    1
                )
            )
            .ReturnsAsync(
                (UserRetailerMembership?)null
            );

        // Act
        var result =
            await _service.RemoveMembershipAsync(
                20,
                1
            );

        // Assert
        Assert.False(result);

        _membershipRepositoryMock.Verify(
            repository =>
                repository.RemoveAsync(
                    It.IsAny<UserRetailerMembership>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RemoveMembershipAsync_WhenMembershipExists_RemovesItAndReturnsTrue()
    {
        // Arrange
        var membership =
            CreateMembership(
                id: 100,
                userId: 20,
                retailerId: 1
            );

        _membershipRepositoryMock
            .Setup(repository =>
                repository.GetByUserAndRetailerAsync(
                    membership.UserId,
                    membership.RetailerId
                )
            )
            .ReturnsAsync(membership);

        _membershipRepositoryMock
            .Setup(repository =>
                repository.RemoveAsync(
                    membership
                )
            )
            .Returns(Task.CompletedTask);

        // Act
        var result =
            await _service.RemoveMembershipAsync(
                membership.UserId,
                membership.RetailerId
            );

        // Assert
        Assert.True(result);

        _membershipRepositoryMock.Verify(
            repository =>
                repository.RemoveAsync(
                    membership
                ),
            Times.Once
        );
    }

    private static Retailer CreateRetailer(
        int id,
        bool supportsMembership)
    {
        return new Retailer
        {
            Id = id,

            Name =
                "Kroger",

            SupportsMembership =
                supportsMembership,

            IsActive =
                true,

            CreatedAt =
                DateTime.UtcNow
        };
    }

    private static UserRetailerMembership
        CreateMembership(
            int id,
            int userId,
            int retailerId)
    {
        return new UserRetailerMembership
        {
            Id = id,

            UserId =
                userId,

            RetailerId =
                retailerId,

            CreatedAt =
                DateTime.UtcNow.AddMinutes(-10)
        };
    }
}