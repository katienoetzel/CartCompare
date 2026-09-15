namespace CartCompare.Services.Models;

public enum ProductPriceSyncResultType
{
    Succeeded,
    ItemNotFound,
    StoreLocationNotFound,
    RetailerNotFound,
    ProviderNotFound,
    ProviderProductNotFound,
    ProductMatchedToDifferentItem,
    PriceNotAvailable,
    PriceSyncFailed
}