namespace CartCompare.Api.Models.Requests;

public class SyncProductPriceRequest
{
    public int ItemId { get; set; }

    public int StoreLocationId { get; set; }

    public required string ExternalProductId { get; set; }
}