using System.Text.Json.Serialization;

namespace CartCompare.Infrastructure.Providers.Kroger.Models;

public class KrogerProductDetailsResponse
{
    [JsonPropertyName("data")]
    public KrogerProduct? Data { get; set; }
}