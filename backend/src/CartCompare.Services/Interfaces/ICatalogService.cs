using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

// Lets shoppers add grocery items that are based on real
// retailer products, so every new item matches something.
public interface ICatalogService
{
    Task<CatalogSearchResult> SearchAsync(
        int storeLocationId, string query);

    // Creates a generic item from the picked product. The name and size
    // may be edited (e.g. to drop the brand) but must still match that
    // product. An existing item with the same name and size is reused.
    Task<CatalogItemResult> CreateItemFromProductAsync(
        int storeLocationId, string externalProductId,
        string name, string size, string? category);
}
