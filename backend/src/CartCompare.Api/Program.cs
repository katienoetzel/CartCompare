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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
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

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT signing key is not configured.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            )
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IRetailerRepository, RetailerRepository>();
builder.Services.AddScoped<IRetailerService, RetailerService>();

builder.Services.AddScoped<ISavedItemRepository, SavedItemRepository>();
builder.Services.AddScoped<ISavedItemService, SavedItemService>();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IItemService, ItemService>();

builder.Services.AddScoped<
    IPriceProviderResolver,
    PriceProviderResolver
>();

builder.Services.AddScoped<KrogerPriceProvider>();

builder.Services.AddScoped<IPriceProvider>(
    provider =>
        provider.GetRequiredService<KrogerPriceProvider>()
);

builder.Services.AddScoped<IStoreLocationRepository, StoreLocationRepository>();
builder.Services.AddScoped<IStoreLocationService, StoreLocationService>();

builder.Services.AddScoped<IProductPriceSyncService, ProductPriceSyncService>();

builder.Services.AddScoped<IStoreComparisonService, StoreComparisonService>();

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

builder.Services.AddScoped<IPriceRepository, PriceRepository>();
builder.Services.AddScoped<IPriceService, PriceService>();

builder.Services.AddScoped<IRetailerProductRepository, RetailerProductRepository>();
builder.Services.AddScoped<IRetailerProductService, RetailerProductService>();

builder.Services.AddScoped<IGroceryListRepository, GroceryListRepository>();
builder.Services.AddScoped<IGroceryListService, GroceryListService>();

builder.Services.AddScoped<IAuthService, AuthService>();

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

builder.Services.AddScoped<
    IUserRetailerMembershipRepository,
    UserRetailerMembershipRepository
>();

builder.Services.AddScoped<
    IUserRetailerMembershipService,
    UserRetailerMembershipService
>();

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "DevelopmentFrontend",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});

var app = builder.Build();

if (
    builder.Configuration.GetValue<bool>(
        "E2E:Enabled"
    )
)
{
    using var scope =
        app.Services.CreateScope();

    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<
                CartCompareDbContext
            >();

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

    await dbContext.Database
        .MigrateAsync();
}

app.UseCors("DevelopmentFrontend");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }