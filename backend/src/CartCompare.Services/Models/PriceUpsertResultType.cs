namespace CartCompare.Services.Models;

public enum PriceUpsertResultType
{
    Created,
    Updated,
    RetailerProductNotFound,
    StoreLocationNotFound,
    RetailerMismatch,
    InvalidPrice
}