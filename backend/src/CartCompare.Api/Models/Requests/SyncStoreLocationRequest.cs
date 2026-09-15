namespace CartCompare.Api.Models.Requests;

public class SyncStoreLocationsRequest
{
    public int RetailerId { get; set; }

    public required string PostalCode { get; set; }
}