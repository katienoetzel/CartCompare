namespace CartCompare.Providers.Models;

public class ProviderProduct
{
    public required string ExternalProductId { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Upc { get; set; }

    // The lowest price the search listing showed. It only orders
    // candidates; the comparison always uses a freshly synced price.
    public decimal? ShelfPrice { get; set; }
}