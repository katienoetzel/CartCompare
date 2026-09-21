using System.Text;

using CartCompare.Infrastructure.Data;
using CartCompare.IntegrationTests.Fakes;
using CartCompare.Providers.Interfaces;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using Npgsql;

namespace CartCompare.IntegrationTests.Fixtures;

public class CartCompareWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private const string TestJwtKey =
        "CartCompareIntegrationTestsOnlyJwtKey_2026_DoNotUseInProduction_123456789";

    private const string TestJwtIssuer =
        "CartCompare.IntegrationTests";

    private const string TestJwtAudience =
        "CartCompare.IntegrationTests";

    public const string TestAdminEmail =
        "cartcompare-admin@example.com";

    private readonly string
        _connectionString;

    private readonly bool
        _strictAdminAuthorization;

    public FakePriceProvider
        TestPriceProvider
    { get; } =
            new();

    public FakeKrogerHttpMessageHandler
        TestKrogerHttpHandler
    { get; } =
            new();

    public CartCompareWebApplicationFactory(
        string connectionString,
        bool strictAdminAuthorization = false)
    {
        ValidateTestDatabase(
            connectionString
        );

        _connectionString =
            connectionString;

        _strictAdminAuthorization =
            strictAdminAuthorization;
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (context, configurationBuilder) =>
            {
                var testSettings =
                    new Dictionary<string, string?>
                    {
                        [
                            "ConnectionStrings:DefaultConnection"
                        ] =
                            _connectionString,

                        [
                            "Jwt:Key"
                        ] =
                            TestJwtKey,

                        [
                            "Jwt:Issuer"
                        ] =
                            TestJwtIssuer,

                        [
                            "Jwt:Audience"
                        ] =
                            TestJwtAudience,

                        [
                            "Admin:Email"
                        ] =
                            TestAdminEmail,

                        [
                            "Kroger:ClientId"
                        ] =
                            "integration-test-client",

                        [
                            "Kroger:ClientSecret"
                        ] =
                            "integration-test-secret",

                        [
                            "Kroger:BaseUrl"
                        ] =
                            "https://kroger.integration.test/"
                    };

                configurationBuilder
                    .AddInMemoryCollection(
                        testSettings
                    );
            }
        );

        builder.ConfigureServices(
            services =>
            {
                // -----------------------------------------
                // Replace production DbContext with
                // cartcompare_test.
                // -----------------------------------------

                var dbContextOptionsDescriptor =
                    services.SingleOrDefault(
                        descriptor =>
                            descriptor.ServiceType ==
                            typeof(
                                DbContextOptions<
                                    CartCompareDbContext
                                >
                            )
                    );

                if (
                    dbContextOptionsDescriptor
                    is not null
                )
                {
                    services.Remove(
                        dbContextOptionsDescriptor
                    );
                }

                services.RemoveAll<
                    CartCompareDbContext
                >();

                services.AddDbContext<
                    CartCompareDbContext
                >(
                    options =>
                        options.UseNpgsql(
                            _connectionString
                        )
                );

                // -----------------------------------------
                // Provider-backed business workflows
                //
                // Keep the REAL PriceProviderResolver,
                // but replace its IPriceProvider list
                // with our deterministic fake.
                // -----------------------------------------

                services.RemoveAll<
                    IPriceProvider
                >();

                services.AddSingleton(
                    TestPriceProvider
                );

                services.AddSingleton<
                    IPriceProvider
                >(
                    provider =>
                        provider.GetRequiredService<
                            FakePriceProvider
                        >()
                );

                // -----------------------------------------
                // Kroger-specific integration endpoints
                //
                // Keep the REAL KrogerTokenService and
                // REAL KrogerPriceProvider, but intercept
                // the named Kroger HttpClient here.
                //
                // No integration test can reach Kroger.
                // -----------------------------------------

                services
                    .AddHttpClient(
                        "Kroger"
                    )
                    .ConfigurePrimaryHttpMessageHandler(
                        () =>
                            TestKrogerHttpHandler
                    );

                // -----------------------------------------
                // Force JWT validation to use the same
                // test-only JWT settings used to create
                // tokens.
                // -----------------------------------------

                services.PostConfigure<
                    JwtBearerOptions
                >(
                    JwtBearerDefaults
                        .AuthenticationScheme,

                    options =>
                    {
                        options.TokenValidationParameters =
                            new TokenValidationParameters
                            {
                                ValidateIssuer =
                                    true,

                                ValidateAudience =
                                    true,

                                ValidateLifetime =
                                    true,

                                ValidateIssuerSigningKey =
                                    true,

                                ValidIssuer =
                                    TestJwtIssuer,

                                ValidAudience =
                                    TestJwtAudience,

                                IssuerSigningKey =
                                    new SymmetricSecurityKey(
                                        Encoding.UTF8
                                            .GetBytes(
                                                TestJwtKey
                                            )
                                    ),

                                ClockSkew =
                                    TimeSpan.Zero
                            };
                    }
                );

                // -----------------------------------------
                // Admin authorization
                //
                // Most existing API tests are testing
                // controller/service behavior rather than
                // administrator permissions.
                //
                // In normal test-factory mode, AdminOnly
                // therefore requires authentication but
                // does not require the special admin email.
                //
                // Dedicated security tests use
                // strictAdminAuthorization = true so that
                // the REAL AdminOnly policy configured in
                // Program.cs is exercised.
                // -----------------------------------------

                if (!_strictAdminAuthorization)
                {
                    services.PostConfigure<
                        AuthorizationOptions
                    >(
                        options =>
                        {
                            options.AddPolicy(
                                "AdminOnly",
                                policy =>
                                {
                                    policy
                                        .RequireAuthenticatedUser();
                                }
                            );
                        }
                    );
                }
            }
        );
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
                "API integration tests refused "
                + "to use database "
                + $"'{builder.Database}'. "
                + "The database must be named "
                + "'cartcompare_test'."
            );
        }
    }
}