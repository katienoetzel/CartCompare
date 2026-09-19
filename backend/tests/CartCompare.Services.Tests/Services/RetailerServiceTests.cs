using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class RetailerServiceTests
{
    private readonly Mock<IRetailerRepository>
        _retailerRepositoryMock;

    private readonly RetailerService
        _service;

    public RetailerServiceTests()
    {
        _retailerRepositoryMock =
            new Mock<IRetailerRepository>();

        _service =
            new RetailerService(
                _retailerRepositoryMock.Object
            );
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveRetailersFromRepository()
    {
        // Arrange
        var retailers =
            new List<Retailer>
            {
                CreateRetailer(
                    id: 1,
                    name: "Kroger"
                ),

                CreateRetailer(
                    id: 2,
                    name: "Harris Teeter"
                )
            };

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetActiveAsync()
            )
            .ReturnsAsync(retailers);

        // Act
        var result =
            await _service.GetActiveAsync();

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.Same(
            retailers,
            result
        );

        _retailerRepositoryMock.Verify(
            repository =>
                repository.GetActiveAsync(),
            Times.Once
        );
    }

    [Fact]
    public async Task GetActiveAsync_WhenNoRetailersExist_ReturnsEmptyList()
    {
        // Arrange
        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetActiveAsync()
            )
            .ReturnsAsync(
                new List<Retailer>()
            );

        // Act
        var result =
            await _service.GetActiveAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRetailerExists_ReturnsRetailer()
    {
        // Arrange
        var retailer =
            CreateRetailer(
                id: 1,
                name: "Kroger"
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
            await _service.GetByIdAsync(
                retailer.Id
            );

        // Assert
        Assert.Same(
            retailer,
            result
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenRetailerDoesNotExist_ReturnsNull()
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
            await _service.GetByIdAsync(
                999
            );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_CreatesRetailerWithTrimmedNameAndTimestamp()
    {
        // Arrange
        Retailer? addedRetailer =
            null;

        _retailerRepositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<Retailer>()
                )
            )
            .Callback<Retailer>(
                retailer =>
                {
                    retailer.Id = 100;
                    addedRetailer = retailer;
                }
            )
            .Returns(Task.CompletedTask);

        var before =
            DateTime.UtcNow;

        // Act
        var result =
            await _service.CreateAsync(
                "  Kroger  ",
                true
            );

        var after =
            DateTime.UtcNow;

        // Assert
        Assert.NotNull(
            addedRetailer
        );

        Assert.Equal(
            "Kroger",
            addedRetailer.Name
        );

        Assert.True(
            addedRetailer.SupportsMembership
        );

        Assert.True(
            addedRetailer.IsActive
        );

        Assert.InRange(
            addedRetailer.CreatedAt,
            before,
            after
        );

        Assert.Same(
            addedRetailer,
            result
        );

        _retailerRepositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<Retailer>()
                ),
            Times.Once
        );
    }

    private static Retailer CreateRetailer(
        int id,
        string name)
    {
        return new Retailer
        {
            Id = id,
            Name = name,
            SupportsMembership = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}