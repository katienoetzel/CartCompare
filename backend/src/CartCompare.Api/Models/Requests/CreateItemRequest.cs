namespace CartCompare.Api.Models.Requests;

public class CreateItemRequest
{
    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Category { get; set; }
}