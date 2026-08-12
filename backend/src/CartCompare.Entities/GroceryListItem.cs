namespace CartCompare.Entities;

public class GroceryListItem
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ItemId { get; set; }

    public int Quantity { get; set; } = 1;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}