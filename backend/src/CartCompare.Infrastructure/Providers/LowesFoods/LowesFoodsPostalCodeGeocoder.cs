using System.Globalization;
using System.Net;
using System.Text.Json;

namespace CartCompare.Infrastructure.Providers.LowesFoods;

public readonly record struct LowesFoodsCoordinates(
    decimal Latitude,
    decimal Longitude
);

public sealed class LowesFoodsPostalCodeGeocoder
{
    public const string ClientName = "LowesFoodsPostalLookup";

    private readonly IHttpClientFactory _httpClientFactory;

    public LowesFoodsPostalCodeGeocoder(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<LowesFoodsCoordinates?> ResolveAsync(
        string postalCode)
    {
        var zip = postalCode.Trim();

        if (zip.Length != 5 || !zip.All(char.IsAsciiDigit))
        {
            return null;
        }

        using var client =
            _httpClientFactory.CreateClient(ClientName);

        using var response =
            await client.GetAsync($"us/{zip}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(stream);

        if (!document.RootElement.TryGetProperty(
                "places", out var places)
            || places.ValueKind != JsonValueKind.Array
            || places.GetArrayLength() == 0)
        {
            throw new InvalidDataException(
                "The postal lookup returned no coordinates."
            );
        }

        var place = places[0];

        if (!TryReadCoordinate(place, "latitude", out var latitude)
            || !TryReadCoordinate(place, "longitude", out var longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            throw new InvalidDataException(
                "The postal lookup returned invalid coordinates."
            );
        }

        return new LowesFoodsCoordinates(latitude, longitude);
    }

    private static bool TryReadCoordinate(
        JsonElement place,
        string name,
        out decimal value)
    {
        value = default;

        return place.TryGetProperty(name, out var element)
            && element.ValueKind is JsonValueKind.String or JsonValueKind.Number
            && decimal.TryParse(
                element.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value
            );
    }
}
