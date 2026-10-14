using System.Security.Claims;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace CartCompare.Api;

// Comparisons and searches call retailer APIs on our behalf, and login
// accepts passwords, so each is limited per user (or per IP address
// before login). Limits come from "RateLimits:*" configuration.
public static class RateLimitPolicies
{
    public const string Comparisons = "comparisons";
    public const string RetailerSearch = "retailer-search";
    public const string Auth = "auth";

    public static IServiceCollection AddCartCompareRateLimits(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode =
                StatusCodes.Status429TooManyRequests;

            options.OnRejected = (context, cancellationToken) =>
                new ValueTask(
                    context.HttpContext.Response.WriteAsJsonAsync(
                        new { message = "Too many requests. Wait a minute and try again." },
                        cancellationToken
                    )
                );

            AddPerMinute(options, Comparisons,
                configuration.GetValue("RateLimits:ComparisonsPerMinute", 6));
            AddPerMinute(options, RetailerSearch,
                configuration.GetValue("RateLimits:RetailerSearchesPerMinute", 20));
            AddPerMinute(options, Auth,
                configuration.GetValue("RateLimits:AuthPerMinute", 10));
        });
    }

    private static void AddPerMinute(
        RateLimiterOptions options,
        string policyName,
        int permitsPerMinute)
    {
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }
            )
        );
    }

    private static string PartitionKey(
        HttpContext httpContext)
    {
        var userId =
            httpContext.User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        return userId is not null
            ? $"user:{userId}"
            : $"ip:{httpContext.Connection.RemoteIpAddress}";
    }
}
