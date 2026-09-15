using CartCompare.Entities;

namespace CartCompare.Services.Models;

public class ProductPriceSyncResult
{
    public ProductPriceSyncResultType Result { get; set; }

    public RetailerProduct? RetailerProduct { get; set; }

    public Price? Price { get; set; }

    public bool ProductCreated { get; set; }

    public bool ProductUpdated { get; set; }

    public bool PriceSynced { get; set; }

    public string? ProviderName { get; set; }
}