using System.Net.Http.Headers;
using System.Text.Json;
using CartCompare.Infrastructure.Providers.Kroger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartCompare.Api.Controllers;

[ApiController]
[Route("api/dev/kroger")]
[AllowAnonymous]
public sealed class KrogerDevelopmentController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly KrogerTokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;

    public KrogerDevelopmentController(
        IWebHostEnvironment environment,
        KrogerTokenService tokenService,
        IHttpClientFactory httpClientFactory)
    {
        _environment = environment;
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("nearby-chains")]
    public async Task<IActionResult> GetNearbyChains(
        [FromQuery] string? postalCode)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var zip = postalCode?.Trim();
        if (zip is null || zip.Length != 5 ||
            !zip.All(char.IsAsciiDigit))
        {
            return BadRequest(new
            {
                message = "A five-digit US postal code is required."
            });
        }

        var accessToken = await _tokenService.GetAccessTokenAsync();
        var client = _httpClientFactory.CreateClient("Kroger");
        var url = "v1/locations?filter.zipCode.near=" +
            Uri.EscapeDataString(zip) + "&filter.limit=200";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new KrogerApiException("locations", response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var locations = document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();

        var chains = locations
            .Select(location =>
                location.TryGetProperty("chain", out var chain)
                    ? chain.GetString() ?? "(missing)"
                    : "(missing)")
            .GroupBy(chain => chain, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                name = group.Key,
                count = group.Count()
            })
            .OrderByDescending(group => group.count)
            .ToList();

        return Ok(new
        {
            postalCode = zip,
            locationsReturned = locations.Length,
            chains
        });
    }
}
