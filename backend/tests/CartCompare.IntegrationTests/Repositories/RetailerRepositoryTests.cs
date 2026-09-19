using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Repositories;

public class RetailerRepositoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_PersistsRetailer()
    {
        // Arrange
        var retailer =
            new Retailer
            {
                Name = "Kroger",
                SupportsMembership = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

        await using (
            var writeContext =
                CreateDbContext())
        {
            var writeRepository =
                new RetailerRepository(
                    writeContext
                );

            // Act
            await writeRepository.AddAsync(
                retailer
            );
        }

        // Assert using a fresh DbContext.
        await using var readContext =
            CreateDbContext();

        var stored =
            await readContext
                .Set<Retailer>()
                .SingleAsync(
                    row =>
                        row.Id ==
                        retailer.Id
                );

        Assert.True(
            stored.Id > 0
        );

        Assert.Equal(
            "Kroger",
            stored.Name
        );

        Assert.True(
            stored.SupportsMembership
        );

        Assert.True(
            stored.IsActive
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenRetailerExists_ReturnsRetailer()
    {
        // Arrange
        var retailerId =
            await SeedRetailerAsync(
                name: "Kroger",
                isActive: true
            );

        await using var context =
            CreateDbContext();

        var repository =
            new RetailerRepository(
                context
            );

        // Act
        var result =
            await repository.GetByIdAsync(
                retailerId
            );

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            retailerId,
            result.Id
        );

        Assert.Equal(
            "Kroger",
            result.Name
        );
    }

    [Fact]
    public async Task GetByIdAsync_WhenRetailerDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context =
            CreateDbContext();

        var repository =
            new RetailerRepository(
                context
            );

        // Act
        var result =
            await repository.GetByIdAsync(
                999999
            );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveRetailers()
    {
        // Arrange
        var activeRetailerId =
            await SeedRetailerAsync(
                name: "Kroger",
                isActive: true
            );

        var secondActiveRetailerId =
            await SeedRetailerAsync(
                name: "Harris Teeter",
                isActive: true
            );

        var inactiveRetailerId =
            await SeedRetailerAsync(
                name: "Inactive Retailer",
                isActive: false
            );

        await using var context =
            CreateDbContext();

        var repository =
            new RetailerRepository(
                context
            );

        // Act
        var result =
            await repository.GetActiveAsync();

        // Assert
        Assert.Equal(
            2,
            result.Count
        );

        Assert.Contains(
            result,
            retailer =>
                retailer.Id ==
                activeRetailerId
        );

        Assert.Contains(
            result,
            retailer =>
                retailer.Id ==
                secondActiveRetailerId
        );

        Assert.DoesNotContain(
            result,
            retailer =>
                retailer.Id ==
                inactiveRetailerId
        );

        Assert.All(
            result,
            retailer =>
                Assert.True(
                    retailer.IsActive
                )
        );
    }

    private async Task<int> SeedRetailerAsync(
        string name,
        bool isActive)
    {
        await using var context =
            CreateDbContext();

        var retailer =
            new Retailer
            {
                Name = name,

                SupportsMembership =
                    true,

                IsActive =
                    isActive,

                CreatedAt =
                    DateTime.UtcNow
            };

        context.Set<Retailer>()
            .Add(retailer);

        await context.SaveChangesAsync();

        return retailer.Id;
    }
}