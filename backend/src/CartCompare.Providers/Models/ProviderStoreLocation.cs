namespace CartCompare.Providers.Models;

public class ProviderStoreLocation
{
    public required string ExternalLocationId { get; set; }

    public string? Name { get; set; }

    public required string AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public required string City { get; set; }

    public required string State { get; set; }

    public required string PostalCode { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }
}