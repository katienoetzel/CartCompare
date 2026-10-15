using System.Text.Json.Serialization;
using CartCompare.Entities;

namespace CartCompare.Services.Models;

// A product found in a retailer's catalog, offered as the basis
// for a grocery item.
public class CatalogProduct
{
    public required string ExternalProductId { get; set; }
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public string? Size { get; set; }
    public decimal? ShelfPrice { get; set; }
}

// Sent as "Branded" / "Generic" in JSON.
[JsonConverter(typeof(JsonStringEnumConverter<CatalogItemKind>))]
public enum CatalogItemKind
{
    // Matches only this brand at every store ("Doritos" chips).
    Branded,

    // Matches any brand whose product fits the name and size.
    Generic
}

// A new item the server can build from a picked product. Shoppers
// choose an option; they never type its name, brand or size.
public class CatalogItemOption
{
    public CatalogItemKind Kind { get; set; }
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public required string Size { get; set; }
}

public enum CatalogResultType
{
    Succeeded,
    StoreNotFound,
    ProviderNotFound,
    ProductNotFound,

    // The requested kind cannot be built safely from this product.
    OptionNotAvailable
}

public class CatalogSearchResult
{
    public CatalogResultType Result { get; set; }
    public string? RetailerName { get; set; }
    public List<CatalogProduct> Products { get; set; } = new();
}

public class CatalogProductOptions
{
    public CatalogResultType Result { get; set; }
    public CatalogProduct? Product { get; set; }

    // Catalog items this product already counts as; adding one of
    // these avoids creating a duplicate.
    public List<Item> ExistingItems { get; set; } = new();

    // New items that could be created; none duplicates an existing one.
    public List<CatalogItemOption> NewItems { get; set; } = new();
}

public class CatalogItemResult
{
    public CatalogResultType Result { get; set; }
    public Item? Item { get; set; }

    // False when an equivalent item already existed and was reused.
    public bool Created { get; set; }
}
