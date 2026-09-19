using System.Net;
using System.Text.Json;
using CartCompare.Entities;
using CartCompare.IntegrationTests.Fixtures;

namespace CartCompare.IntegrationTests.Api;

public class ApiFactoryTests
    : PostgresIntegrationTestBase
{
    [Fact]
    public async Task ApiFactory_StartsAndReadsFromTestDatabase()
    {
        // Arrange
        await using (
            var context =
                CreateDbContext())
        {
            var now =
                DateTime.UtcNow;

            context.Set<Item>()
                .Add(
                    new Item
                    {
                        Name =
                            "Integration Test Milk",

                        Brand =
                            "Test Brand",

                        Size =
                            "1 gallon",

                        Category =
                            "Dairy",

                        IsActive =
                            true,

                        CreatedAt =
                            now,

                        UpdatedAt =
                            now
                    }
                );

            await context.SaveChangesAsync();
        }

        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        // Act
        var response =
            await client.GetAsync(
                "/api/items"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(
                json
            );

        Assert.Equal(
            JsonValueKind.Array,
            document.RootElement.ValueKind
        );

        Assert.Equal(
            1,
            document.RootElement
                .GetArrayLength()
        );

        var firstItem =
            document.RootElement[0];

        Assert.Equal(
            "Integration Test Milk",
            firstItem
                .GetProperty("name")
                .GetString()
        );
    }
}