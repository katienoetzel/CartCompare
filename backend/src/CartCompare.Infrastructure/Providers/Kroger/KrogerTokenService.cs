using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CartCompare.Infrastructure.Providers.Kroger.Models;
using Microsoft.Extensions.Configuration;

namespace CartCompare.Infrastructure.Providers.Kroger;

public class KrogerTokenService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    private string? _accessToken;
    private DateTime _expiresAtUtc;

    public KrogerTokenService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (
            !string.IsNullOrWhiteSpace(_accessToken) &&
            DateTime.UtcNow < _expiresAtUtc)
        {
            return _accessToken;
        }

        var clientId =
            _configuration["Kroger:ClientId"];

        var clientSecret =
            _configuration["Kroger:ClientSecret"];

        if (
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Kroger API credentials are not configured."
            );
        }

        var credentials =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    $"{clientId}:{clientSecret}"
                )
            );

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "v1/connect/oauth2/token"
            );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                credentials
            );

        request.Content =
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] =
                        "client_credentials",

                    ["scope"] =
                        "product.compact"
                }
            );

        var httpClient =
    _httpClientFactory.CreateClient("Kroger");

        using var response =
            await httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var tokenResponse =
            await response.Content
                .ReadFromJsonAsync<KrogerTokenResponse>();

        if (
            tokenResponse is null ||
            string.IsNullOrWhiteSpace(
                tokenResponse.AccessToken
            ))
        {
            throw new InvalidOperationException(
                "Kroger returned an invalid token response."
            );
        }

        _accessToken = tokenResponse.AccessToken;

        _expiresAtUtc =
            DateTime.UtcNow.AddSeconds(
                Math.Max(
                    tokenResponse.ExpiresIn - 60,
                    0
                )
            );

        return _accessToken;
    }
}