namespace CartCompare.Entities;

public class Item
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}