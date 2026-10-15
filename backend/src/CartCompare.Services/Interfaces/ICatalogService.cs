using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

// Lets shoppers add grocery items that are built from real retailer
// products, so every new item matches something and no names are typed.
public interface ICatalogService
{
    // Searches every retailer, each at one store it already knows: an
    // item only needs the product's name, brand and size, which are
    // the same across a chain, so the shopper's location isn't needed.
    Task<CatalogSearchResult> SearchAsync(
        string query);

    // What the picked product could become: existing catalog items it
    // already matches, and new branded or generic items.
    Task<CatalogProductOptions> GetOptionsAsync(
        int storeLocationId, string externalProductId);

    // Creates the chosen kind of item for the product, or reuses an
    // equivalent existing item. Records the shopper who added it.
    Task<CatalogItemResult> CreateItemAsync(
        int storeLocationId, string externalProductId,
        CatalogItemKind kind, int createdByUserId);
}
