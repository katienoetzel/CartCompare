using System.Text.Json.Serialization;

namespace CartCompare.Entities;

public class Item
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    // The shopper who added the item from a store product, so an admin
    // can find and deactivate odd entries. Null for admin-created items.
    // Kept out of API responses: the item list is public.
    [JsonIgnore]
    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}