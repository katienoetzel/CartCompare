using System.ComponentModel.DataAnnotations;

namespace CartCompare.Api.Models.Requests;

public class CreateStoreLocationRequest
{
    public int RetailerId { get; set; }

    public required string ExternalLocationId { get; set; }

    public string? Name { get; set; }

    public required string AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public required string City { get; set; }

    public required string State { get; set; }

    public required string PostalCode { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }
}