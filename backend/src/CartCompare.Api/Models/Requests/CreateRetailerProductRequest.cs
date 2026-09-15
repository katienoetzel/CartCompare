namespace CartCompare.Api.Models.Requests;

public class CreateRetailerProductRequest
{
    public int ItemId { get; set; }

    public int RetailerId { get; set; }

    public required string ExternalProductId { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Upc { get; set; }
}