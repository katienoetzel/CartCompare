using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.Configuration;

namespace CartCompare.Infrastructure.Providers.LowesFoods;

public sealed class LowesFoodsSessionClient
{
    private const string TenantId =
        "2c847eb7-9043-4340-822e-6184f2a695e8";

    private const string BannerId =
        "shop_lowes_foods";

    private readonly Uri _baseUri;

    public LowesFoodsSessionClient(
        IConfiguration configuration)
    {
        var baseUrl =
            configuration[
                "LowesFoods:BaseUrl"
            ]
            ?? "https://falcon.shop.inmar.io/";

        _baseUri =
            new Uri(
                baseUrl.EndsWith("/")
                    ? baseUrl
                    : baseUrl + "/"
            );
    }

    public async Task<JsonDocument>
        GetLocationsAsync(
            decimal latitude,
            decimal longitude)
    {
        using var handler =
            CreateHandler();

        using var client =
            CreateClient(
                handler
            );

        await InitializeSessionAsync(
            client,
            externalLocationId: null
        );

        var path = FormattableString.Invariant(
            $"v2/locations?fulfillmentMethod=pickup&lat={latitude}&lon={longitude}"
        );

        using var response =
            await client.GetAsync(path);

        await EnsureSuccessAsync(
            response,
            "Lowes Foods location lookup"
        );

        return await ReadJsonAsync(
            response
        );
    }

    public async Task<JsonDocument>
        SearchAsync(
            string externalLocationId,
            string query)
    {
        if (
            string.IsNullOrWhiteSpace(
                externalLocationId
            )
        )
        {
            throw new ArgumentException(
                "A Lowes Foods location ID is required.",
                nameof(externalLocationId)
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                query
            )
        )
        {
            throw new ArgumentException(
                "A Lowes Foods search query is required.",
                nameof(query)
            );
        }

        using var handler =
            CreateHandler();

        using var client =
            CreateClient(
                handler
            );

        await InitializeSessionAsync(
            client,
            externalLocationId.Trim()
        );

        await SelectLocationAsync(
            client,
            externalLocationId.Trim()
        );

        var encodedQuery =
            Uri.EscapeDataString(
                query.Trim()
            );

        using var response =
            await client.GetAsync(
                $"v2/search?searchTerms={encodedQuery}"
            );

        await EnsureSuccessAsync(
            response,
            "Lowes Foods product search"
        );

        return await ReadJsonAsync(
            response
        );
    }

    private static HttpClientHandler
        CreateHandler()
    {
        return new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,

            CookieContainer =
                new CookieContainer(),

            UseCookies = true
        };
    }

    private HttpClient CreateClient(
        HttpClientHandler handler)
    {
        var client =
            new HttpClient(
                handler,
                disposeHandler: true
            )
            {
                BaseAddress =
                    _baseUri,

                Timeout =
                    TimeSpan.FromSeconds(
                        30
                    )
            };

        client.DefaultRequestHeaders
            .Accept
            .Add(
                new MediaTypeWithQualityHeaderValue(
                    "*/*"
                )
            );

        client.DefaultRequestHeaders
            .TryAddWithoutValidation(
                "Origin",
                "https://shop.lowesfoods.com"
            );

        client.DefaultRequestHeaders
            .Referrer =
                new Uri(
                    "https://shop.lowesfoods.com/"
                );

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "Mozilla/5.0 "
                + "(Windows NT 10.0; Win64; x64) "
                + "AppleWebKit/537.36 "
                + "(KHTML, like Gecko) "
                + "Chrome/154.0.0.0 "
                + "Safari/537.36"
            );

        // The browser sends these on the POST that
        // creates the Inmar session. It does NOT send
        // inmar-session-id until after the session exists.
        client.DefaultRequestHeaders
            .TryAddWithoutValidation(
                "inmar-banner-id",
                BannerId
            );

        client.DefaultRequestHeaders
            .TryAddWithoutValidation(
                "x-correlation-id",
                CreateOpaqueId()
            );

        return client;
    }

    private static async Task
        InitializeSessionAsync(
            HttpClient client,
            string? externalLocationId)
    {
        var requestedSessionId =
            CreateOpaqueId();

        // The browser creates/registers the session by
        // POSTing a client-generated sessionId in the body.
        // We intentionally do not send inmar-session-id
        // on this first POST because the session does not
        // exist yet.
        var createSessionPayload =
            new
            {
                data =
                    new
                    {
                        item =
                            new
                            {
                                sessionId =
                                    requestedSessionId,

                                cart =
                                    new
                                    {
                                        products =
                                            new { }
                                    },

                                sessionTags =
                                    CreateSessionTags(
                                        externalLocationId
                                    )
                            }
                    }
            };

        using var createResponse =
            await client.PostAsJsonAsync(
                "v2/session",
                createSessionPayload
            );

        await EnsureSuccessAsync(
            createResponse,
            "Lowes Foods session creation"
        );

        var actualSessionId =
            await TryReadSessionIdAsync(
                createResponse
            )
            ??
            requestedSessionId;

        client.DefaultRequestHeaders
            .TryAddWithoutValidation(
                "inmar-session-id",
                actualSessionId
            );

        await BootstrapConfigurationAsync(
            client
        );

        using var sessionResponse =
            await client.GetAsync(
                "v2/session"
            );

        await EnsureSuccessAsync(
            sessionResponse,
            "Lowes Foods session initialization"
        );
    }

    private static object
        CreateSessionTags(
            string? externalLocationId)
    {
        // The real browser payload includes more cached
        // display metadata and a currently selected pickup
        // slot. Those values are transient. The stable
        // pieces needed to establish the session are the
        // tenant, fulfillment type, and (when known) the
        // location ID. /v2/fulfillmentInfo is called next
        // to make the selected store authoritative.
        if (
            string.IsNullOrWhiteSpace(
                externalLocationId
            )
        )
        {
            return new
            {
                selectedOrderFulfillmentType =
                    "pickup",

                tenantId =
                    TenantId
            };
        }

        return new
        {
            selectedOrderFulfillmentType =
                "pickup",

            locationId =
                externalLocationId,

            tenantId =
                TenantId
        };
    }

    private static async Task
        BootstrapConfigurationAsync(
            HttpClient client)
    {
        var configurationPayload =
            new
            {
                configurationKeyPrefixes =
                    new[]
                    {
                        "apps/consumer/raven/logs/",
                        "providers/authentication/",
                        "ice/",
                        "apps/consumer/raven/analytics/"
                    }
            };

        using var response =
            await client.PostAsJsonAsync(
                "v2/configuration",
                configurationPayload
            );

        await EnsureSuccessAsync(
            response,
            "Lowes Foods configuration bootstrap"
        );
    }

    private static async Task
        SelectLocationAsync(
            HttpClient client,
            string externalLocationId)
    {
        var payload =
            new
            {
                fulfillmentInPersonConfig =
                    new
                    {
                        locationId =
                            externalLocationId
                    },

                fulfillmentType =
                    "pickup"
            };

        using var response =
            await client.PostAsJsonAsync(
                "v2/fulfillmentInfo",
                payload
            );

        await EnsureSuccessAsync(
            response,
            "Lowes Foods location selection"
        );
    }

    private static async Task<string?>
        TryReadSessionIdAsync(
            HttpResponseMessage response)
    {
        var content =
            await response.Content
                .ReadAsStringAsync();

        if (
            string.IsNullOrWhiteSpace(
                content
            )
        )
        {
            return null;
        }

        try
        {
            using var document =
                JsonDocument.Parse(
                    content
                );

            if (
                document.RootElement
                    .TryGetProperty(
                        "data",
                        out var data
                    )
                &&
                data.TryGetProperty(
                    "item",
                    out var item
                )
                &&
                item.TryGetProperty(
                    "sessionId",
                    out var sessionId
                )
                &&
                sessionId.ValueKind
                ==
                JsonValueKind.String
            )
            {
                return sessionId
                    .GetString();
            }
        }
        catch (
            JsonException
        )
        {
            // Some successful session-creation responses
            // may have no JSON body. In that case the
            // client-generated ID remains authoritative.
        }

        return null;
    }

    private static string
        CreateOpaqueId()
    {
        var bytes =
            RandomNumberGenerator
                .GetBytes(16);

        return Convert
            .ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static async Task
        EnsureSuccessAsync(
            HttpResponseMessage response,
            string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body =
            await response.Content
                .ReadAsStringAsync();

        if (body.Length > 500)
        {
            body =
                body[..500];
        }

        throw new HttpRequestException(
            $"{operation} failed with "
            + $"HTTP {(int)response.StatusCode} "
            + $"({response.StatusCode}). "
            + body
        );
    }

    private static async Task<JsonDocument>
        ReadJsonAsync(
            HttpResponseMessage response)
    {
        await using var stream =
            await response.Content
                .ReadAsStreamAsync();

        return await JsonDocument
            .ParseAsync(
                stream
            );
    }
}
