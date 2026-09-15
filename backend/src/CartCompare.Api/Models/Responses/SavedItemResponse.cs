namespace CartCompare.Api.Models.Responses;

public class SavedItemResponse
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Category { get; set; }

    public DateTime CreatedAt { get; set; }
}