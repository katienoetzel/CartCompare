namespace CartCompare.Services.Models;

public class StoreLocationSyncResult
{
    public StoreLocationSyncResultType Result { get; set; }

    public int RetailerId { get; set; }

    public required string RetailerName { get; set; }

    public string? ProviderName { get; set; }

    public int ProviderStoreCount { get; set; }

    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }
}