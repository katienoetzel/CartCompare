using System.Security.Claims;
using System.Text;

using CartCompare.Api;
using CartCompare.Api.Authentication;
using CartCompare.Entities;
using CartCompare.Infrastructure.Data;
using CartCompare.Infrastructure.Providers;
using CartCompare.Infrastructure.Providers.Kroger;
using CartCompare.Infrastructure.Providers.LowesFoods;
using CartCompare.Providers.Interfaces;
using CartCompare.Repositories.Interfaces;
using CartCompare.Repositories.Repositories;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Cloud Run collects stdout; one JSON object per line keeps the
// structured fields (Provider, Operation, DurationMs, ...) searchable.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

// ==================================================
// Controllers
// ==================================================

builder.Services.AddControllers(options =>
{
    // Outside Development the *DevelopmentController classes are not
    // routed at all, so no action can forget its own environment check.
    if (!builder.Environment.IsDevelopment())
    {
        options.Conventions.Add(
            new RemoveDevelopmentControllersConvention()
        );
    }
});

builder.Services.AddCartCompareRateLimits(
    builder.Configuration
);

// Behind Cloud Run's proxy every request arrives over HTTP from Google's
// address. Trusting X-Forwarded-For/-Proto restores the client IP (rate
// limits before login are per IP) and the HTTPS scheme. Set
// ForwardedHeaders:Enabled only behind a proxy: otherwise clients could
// forge their address. The middleware is added in the HTTP pipeline.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    // The proxy appends the address it saw; take only that one.
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});


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

// HMAC-SHA256 needs a 256-bit key. Fail at startup rather than at
// the first login.
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must be at least 32 bytes."
    );
}

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
    ICatalogService,
    CatalogService
>();

builder.Services.AddScoped<
    IPriceProviderResolver,
    PriceProviderResolver
>();

builder.Services.AddScoped<KrogerPriceProvider>();

builder.Services.AddScoped<IPriceProvider>(
    provider =>
        new LoggingPriceProvider(
            provider.GetRequiredService<
                KrogerPriceProvider
            >(),
            provider.GetRequiredService<
                ILogger<LoggingPriceProvider>
            >()
        )
);

builder.Services.AddSingleton<
    LowesFoodsSessionClient
>();

builder.Services.AddHttpClient(
    LowesFoodsPostalCodeGeocoder.ClientName,
    client =>
    {
        var baseUrl =
            builder.Configuration[
                "LowesFoods:PostalLookupBaseUrl"
            ]
            ?? "https://api.zippopotam.us/";

        client.BaseAddress = new Uri(
            baseUrl.EndsWith('/')
                ? baseUrl
                : baseUrl + "/"
        );

        client.Timeout = TimeSpan.FromSeconds(10);
    }
);

builder.Services.AddSingleton<
    LowesFoodsPostalCodeGeocoder
>();

builder.Services.AddSingleton<
    LowesFoodsPriceProvider
>();

builder.Services.AddSingleton<IPriceProvider>(
    provider =>
        new LoggingPriceProvider(
            provider.GetRequiredService<
                LowesFoodsPriceProvider
            >(),
            provider.GetRequiredService<
                ILogger<LoggingPriceProvider>
            >()
        )
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
    IComparisonPriceRefreshService,
    ComparisonPriceRefreshService
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

        // Kroger's firewall refuses requests with no User-Agent from
        // cloud addresses (Cloud Run got 403s; home networks did not),
        // so identify the app and ask for JSON like other clients do.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "CartCompare/1.0 (+https://github.com/katienoetzel/CartCompare)"
        );

        client.DefaultRequestHeaders.Accept.ParseAdd(
            "application/json"
        );
    }
);

builder.Services.AddSingleton<KrogerTokenService>();


// ==================================================
// Lowes Foods / Inmar
// ==================================================
//
// LowesFoodsSessionClient defaults to:
// https://falcon.shop.inmar.io/
//
// It can be overridden with:
// LowesFoods:BaseUrl
//
// No API key or user credential is stored here.
// Each provider operation creates an isolated
// cookie-backed session, selects the requested
// store, performs the operation, then disposes
// that session.


// ==================================================
// OpenAPI
// ==================================================

builder.Services.AddOpenApi();


// ==================================================
// CORS
// ==================================================

// Comma-separated, e.g. "https://cartcompare.vercel.app". Only
// Development falls back to the Vite dev server.
var frontendOrigins =
    (builder.Configuration["Frontend:Origin"]
        ?? (builder.Environment.IsDevelopment()
            ? "http://localhost:5173"
            : throw new InvalidOperationException(
                "Frontend:Origin must be configured outside Development."
            )))
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
                .WithOrigins(
                    frontendOrigins
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

if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// After authentication, so limits apply per signed-in user.
app.UseRateLimiter();

app.MapControllers();

app.Run();


// Required by integration tests that reference
// the generated Program type.
public partial class Program
{
}
