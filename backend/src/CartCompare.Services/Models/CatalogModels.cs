using CartCompare.Entities;

namespace CartCompare.Services.Models;

// A product found in a retailer's catalog, offered as the basis
// for a new generic grocery item.
public class CatalogProduct
{
    public required string ExternalProductId { get; set; }
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public string? Size { get; set; }
    public decimal? ShelfPrice { get; set; }

    // The product name without brand, size and trademark text,
    // e.g. "Creamy Peanut Butter". The user can still edit it.
    public required string SuggestedName { get; set; }
}

public enum CatalogResultType
{
    Succeeded,
    StoreNotFound,
    ProviderNotFound,
    ProductNotFound,
    NameDoesNotMatchProduct
}

public class CatalogSearchResult
{
    public CatalogResultType Result { get; set; }
    public string? RetailerName { get; set; }
    public List<CatalogProduct> Products { get; set; } = new();
}

public class CatalogItemResult
{
    public CatalogResultType Result { get; set; }
    public Item? Item { get; set; }

    // False when an equivalent item already existed and was reused.
    public bool Created { get; set; }
}
