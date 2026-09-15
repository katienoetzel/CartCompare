namespace CartCompare.Services.Models;

public class SavedItemDetails
{
    public int SavedItemId { get; set; }

    public int ItemId { get; set; }

    public required string ItemName { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Category { get; set; }

    public DateTime CreatedAt { get; set; }
}