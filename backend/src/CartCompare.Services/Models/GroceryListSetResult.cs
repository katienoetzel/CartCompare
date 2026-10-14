namespace CartCompare.Services.Models;

public enum GroceryListSetStatus
{
    Saved,
    ItemNotFound,
    ListFull
}

public class GroceryListSetResult
{
    public GroceryListSetStatus Status { get; set; }

    // Set when Status is Saved.
    public GroceryListItemDetails? Item { get; set; }
}
