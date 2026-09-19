using CartCompare.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace CartCompare.IntegrationTests.Fixtures;

public abstract class PostgresIntegrationTestBase
    : IAsyncLifetime
{
    private static readonly SemaphoreSlim DatabaseLock =
        new(1, 1);

    private bool _lockAcquired;

    protected string ConnectionString { get; }

    protected PostgresIntegrationTestBase()
    {
        var configuration =
            new ConfigurationBuilder()
                .AddUserSecrets<PostgresIntegrationTestBase>()
                .Build();

        ConnectionString =
            configuration
                .GetConnectionString(
                    "TestConnection"
                )
            ?? throw new InvalidOperationException(
                "The integration-test connection string "
                + "'ConnectionStrings:TestConnection' "
                + "was not found in User Secrets."
            );

        ValidateTestDatabase(
            ConnectionString
        );
    }

    public async Task InitializeAsync()
    {
        await DatabaseLock.WaitAsync();

        _lockAcquired = true;

        try
        {
            await ResetDatabaseAsync();
        }
        catch
        {
            DatabaseLock.Release();
            _lockAcquired = false;

            throw;
        }
    }

    public Task DisposeAsync()
    {
        if (_lockAcquired)
        {
            DatabaseLock.Release();
            _lockAcquired = false;
        }

        return Task.CompletedTask;
    }

    protected CartCompareDbContext
        CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder
                <CartCompareDbContext>()
                .UseNpgsql(
                    ConnectionString
                )
                .Options;

        return new CartCompareDbContext(
            options
        );
    }

    private async Task ResetDatabaseAsync()
    {
        // Keep cartcompare_test itself alive.
        // Only reset its public schema.
        await using (
            var connection =
                new NpgsqlConnection(
                    ConnectionString
                ))
        {
            await connection.OpenAsync();

            await using var command =
                connection.CreateCommand();

            command.CommandText =
                """
                DROP SCHEMA IF EXISTS public CASCADE;
                CREATE SCHEMA public;
                """;

            await command.ExecuteNonQueryAsync();
        }

        // The schema is now empty, including
        // __EFMigrationsHistory, so apply every
        // real CartCompare migration again.
        await using var context =
            CreateDbContext();

        await context.Database
            .MigrateAsync();
    }

    private static void ValidateTestDatabase(
        string connectionString)
    {
        var builder =
            new NpgsqlConnectionStringBuilder(
                connectionString
            );

        if (!string.Equals(
                builder.Database,
                "cartcompare_test",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Integration tests refused to reset "
                + $"database '{builder.Database}'. "
                + "The database must be named "
                + "'cartcompare_test'."
            );
        }
    }
}