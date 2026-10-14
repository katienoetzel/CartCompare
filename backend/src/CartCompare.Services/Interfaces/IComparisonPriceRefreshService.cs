using CartCompare.Entities;
using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IComparisonPriceRefreshService
{
    Task<ComparisonPriceRefreshResult> RefreshAsync(
        Item item, StoreLocation store, List<RetailerProduct> matchedProducts);
}
