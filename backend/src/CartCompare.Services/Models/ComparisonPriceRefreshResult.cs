namespace CartCompare.Services.Models;

public class ComparisonPriceRefreshResult
{
    // Only these products may contribute to the comparison. Stale prices
    // whose refresh failed must not enter the subtotal or ranking.
    public HashSet<int> UsableProductIds { get; } = new();
    public string Status { get; set; } = "price_unavailable";
    public string? Message { get; set; }
}
