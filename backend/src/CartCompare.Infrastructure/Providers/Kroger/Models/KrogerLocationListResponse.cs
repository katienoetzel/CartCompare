using System.Text.Json.Serialization;

namespace CartCompare.Infrastructure.Providers.Kroger.Models;

public class KrogerLocationListResponse
{
    [JsonPropertyName("data")]
    public List<KrogerLocation>? Data { get; set; }
}

public class KrogerLocation
{
    [JsonPropertyName("locationId")]
    public string? LocationId { get; set; }

    [JsonPropertyName("chain")]
    public string? Chain { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public KrogerAddress? Address { get; set; }

    [JsonPropertyName("geolocation")]
    public KrogerGeolocation? Geolocation { get; set; }
}

public class KrogerAddress
{
    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; }
}

public class KrogerGeolocation
{
    [JsonPropertyName("latitude")]
    public decimal? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public decimal? Longitude { get; set; }
}