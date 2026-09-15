namespace CartCompare.Services.Models;

public class StoreComparisonResult
{
    public int StoreLocationId { get; set; }

    public required string StoreName { get; set; }

    public int RetailerId { get; set; }

    public decimal KnownSubtotal { get; set; }

    public bool IsComplete { get; set; }

    public int? Rank { get; set; }

    public List<StoreComparisonItemResult> Items { get; set; }
        = new List<StoreComparisonItemResult>();

    public int MissingItemCount =>
        Items.Count(item => !item.IsAvailable);
}