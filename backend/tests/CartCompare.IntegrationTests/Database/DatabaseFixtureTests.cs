using CartCompare.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace CartCompare.IntegrationTests.Database;

public class DatabaseFixtureTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task DatabaseFixture_AppliesMigrationsSuccessfully()
    {
        // Arrange
        await using var context =
            CreateDbContext();

        // Act
        var canConnect =
            await context.Database
                .CanConnectAsync();

        var pendingMigrations =
            await context.Database
                .GetPendingMigrationsAsync();

        // Assert
        Assert.True(
            canConnect
        );

        Assert.Empty(
            pendingMigrations
        );
    }
}