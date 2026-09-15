namespace CartCompare.Services.Models;

public class StoreComparisonItemResult
{
    public int ItemId { get; set; }

    public required string ItemName { get; set; }

    public int Quantity { get; set; }

    public int? RetailerProductId { get; set; }

    public string? RetailerProductName { get; set; }

    public decimal? UnitPrice { get; set; }

    public EffectivePriceType? PriceType { get; set; }

    public decimal? LineTotal { get; set; }

    public bool IsAvailable { get; set; }
}