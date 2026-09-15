using System.Text.Json.Serialization;

namespace CartCompare.Infrastructure.Providers.Kroger.Models;

public class KrogerProductListResponse
{
    [JsonPropertyName("data")]
    public List<KrogerProduct>? Data { get; set; }
}

public class KrogerProduct
{
    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("upc")]
    public string? Upc { get; set; }

    [JsonPropertyName("items")]
    public List<KrogerProductItem>? Items { get; set; }
}

public class KrogerProductItem
{
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("inventory")]
    public KrogerInventory? Inventory { get; set; }

    [JsonPropertyName("price")]
    public KrogerItemPrice? Price { get; set; }
}

public class KrogerInventory
{
    [JsonPropertyName("stockLevel")]
    public string? StockLevel { get; set; }
}

public class KrogerItemPrice
{
    [JsonPropertyName("regular")]
    public decimal? Regular { get; set; }

    [JsonPropertyName("promo")]
    public decimal? Promo { get; set; }
}