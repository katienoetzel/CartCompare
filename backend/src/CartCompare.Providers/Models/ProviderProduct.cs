namespace CartCompare.Providers.Models;

public class ProviderProduct
{
    public required string ExternalProductId { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Upc { get; set; }
}