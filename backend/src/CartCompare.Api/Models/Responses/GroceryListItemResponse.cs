namespace CartCompare.Api.Models.Responses;

public class GroceryListItemResponse
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Category { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}