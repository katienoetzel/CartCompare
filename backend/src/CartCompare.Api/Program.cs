using System.Security.Claims;
using System.Text;

using CartCompare.Api.Authentication;
using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Infrastructure.Providers;
using CartCompare.Infrastructure.Providers.Kroger;
using CartCompare.Providers.Interfaces;
using CartCompare.Repositories.Interfaces;
using CartCompare.Repositories.Repositories;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ==================================================
// Controllers
// ==================================================

builder.Services.AddControllers();


// ==================================================
// Database
// ==================================================

var databaseConnectionName =
    builder.Configuration.GetValue<bool>(
        "E2E:Enabled"
    )
        ? "E2EConnection"
        : "DefaultConnection";

var databaseConnectionString =
    builder.Configuration.GetConnectionString(
        databaseConnectionName
    )
    ?? throw new InvalidOperationException(
        $"Connection string '{databaseConnectionName}' is not configured."
    );

builder.Services.AddDbContext<CartCompareDbContext>(
    options =>
        options.UseNpgsql(
            databaseConnectionString
        )
);


// ==================================================
// Identity
// ==================================================

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<CartCompareDbContext>();


// ==================================================
// JWT authentication
// ==================================================

var jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is not configured."
    );

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"];

var jwtAudience =
    builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtKey
                        )
                    )
            };
    });


// ==================================================
// Authorization
// ==================================================

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(
        "AdminOnly",
        policy =>
        {
            policy.RequireAuthenticatedUser();

            policy.RequireAssertion(
                context =>
                {
                    if (
                        context.Resource
                        is not HttpContext httpContext
                    )
                    {
                        return false;
                    }

                    var configuration =
                        httpContext.RequestServices
                            .GetRequiredService<
                                IConfiguration
                            >();

                    var adminEmail =
                        configuration[
                            "Admin:Email"
                        ];

                    if (
                        string.IsNullOrWhiteSpace(
                            adminEmail
                        )
                    )
                    {
                        return false;
                    }

                    return context.User.Claims.Any(
                        claim =>
                            claim.Type ==
                                ClaimTypes.Email
                            &&
                            string.Equals(
                                claim.Value,
                                adminEmail,
                                StringComparison
                                    .OrdinalIgnoreCase
                            )
                    );
                }
            );
        }
    );


// ==================================================
// Repositories and services
// ==================================================

builder.Services.AddScoped<
    IRetailerRepository,
    RetailerRepository
>();

builder.Services.AddScoped<
    IRetailerService,
    RetailerService
>();

builder.Services.AddScoped<
    ISavedItemRepository,
    SavedItemRepository
>();

builder.Services.AddScoped<
    ISavedItemService,
    SavedItemService
>();

builder.Services.AddScoped<
    IJwtTokenService,
    JwtTokenService
>();

builder.Services.AddScoped<
    IItemRepository,
    ItemRepository
>();

builder.Services.AddScoped<
    IItemService,
    ItemService
>();

builder.Services.AddScoped<
    IPriceProviderResolver,
    PriceProviderResolver
>();

builder.Services.AddScoped<KrogerPriceProvider>();

builder.Services.AddScoped<IPriceProvider>(
    provider =>
        provider.GetRequiredService<
            KrogerPriceProvider
        >()
);

builder.Services.AddScoped<
    IStoreLocationRepository,
    StoreLocationRepository
>();

builder.Services.AddScoped<
    IStoreLocationService,
    StoreLocationService
>();

builder.Services.AddScoped<
    IProductPriceSyncService,
    ProductPriceSyncService
>();

builder.Services.AddScoped<
    IStoreComparisonService,
    StoreComparisonService
>();

builder.Services.AddScoped<
    IStoreLocationSyncService,
    StoreLocationSyncService
>();

builder.Services.AddScoped<
    IProductMatchingService,
    ProductMatchingService
>();

builder.Services.AddScoped<
    IProductCandidateService,
    ProductCandidateService
>();

builder.Services.AddScoped<
    IPriceRepository,
    PriceRepository
>();

builder.Services.AddScoped<
    IPriceService,
    PriceService
>();

builder.Services.AddScoped<
    IRetailerProductRepository,
    RetailerProductRepository
>();

builder.Services.AddScoped<
    IRetailerProductService,
    RetailerProductService
>();

builder.Services.AddScoped<
    IGroceryListRepository,
    GroceryListRepository
>();

builder.Services.AddScoped<
    IGroceryListService,
    GroceryListService
>();

builder.Services.AddScoped<
    IAuthService,
    AuthService
>();

builder.Services.AddScoped<
    IUserRetailerMembershipRepository,
    UserRetailerMembershipRepository
>();

builder.Services.AddScoped<
    IUserRetailerMembershipService,
    UserRetailerMembershipService
>();


// ==================================================
// Kroger API
// ==================================================

var krogerBaseUrl =
    builder.Configuration["Kroger:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Kroger base URL is not configured."
    );

builder.Services.AddHttpClient(
    "Kroger",
    client =>
    {
        client.BaseAddress =
            new Uri(krogerBaseUrl);
    }
);

builder.Services.AddSingleton<KrogerTokenService>();


// ==================================================
// OpenAPI
// ==================================================

builder.Services.AddOpenApi();


// ==================================================
// Health checks
// ==================================================

builder.Services.AddHealthChecks();


// ==================================================
// CORS
// ==================================================

var frontendOrigin =
    builder.Configuration[
        "Frontend:Origin"
    ]
    ?? "http://localhost:5173";

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
                .WithOrigins(
                    frontendOrigin
                )
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});


// ==================================================
// Build application
// ==================================================

var app = builder.Build();


// ==================================================
// Database migrations
// ==================================================

var e2eEnabled =
    builder.Configuration.GetValue<bool>(
        "E2E:Enabled"
    );

var applyMigrations =
    builder.Configuration.GetValue<bool>(
        "Database:ApplyMigrations"
    );

if (
    e2eEnabled ||
    applyMigrations
)
{
    using var scope =
        app.Services.CreateScope();

    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<
                CartCompareDbContext
            >();

    // The E2E database is intentionally reset
    // before every E2E test run.
    //
    // This destructive reset is NEVER performed
    // merely because production migrations are
    // enabled.
    if (e2eEnabled)
    {
        var databaseName =
            dbContext.Database
                .GetDbConnection()
                .Database;

        if (
            !string.Equals(
                databaseName,
                "cartcompare_e2e",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                $"Refusing to reset database '{databaseName}'. " +
                "E2E mode must use cartcompare_e2e."
            );
        }

        await dbContext.Database
            .ExecuteSqlRawAsync(
                """
                DROP SCHEMA IF EXISTS public CASCADE;
                CREATE SCHEMA public;
                """
            );
    }

    await dbContext.Database
        .MigrateAsync();
}


// ==================================================
// HTTP pipeline
// ==================================================

app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();


// Required by integration tests that reference
// the generated Program type.
public partial class Program
{
}