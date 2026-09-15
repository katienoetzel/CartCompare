namespace CartCompare.Services.Models;

public class GroceryListItemDetails
{
    public int GroceryListItemId { get; set; }

    public int ItemId { get; set; }

    public required string ItemName { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Category { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}