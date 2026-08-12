using CartCompare.Entities.Enums;

namespace CartCompare.Entities;

public class RetailerProduct
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public int RetailerId { get; set; }

    public required string ExternalProductId { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Upc { get; set; }

    public ProductMatchMethod MatchMethod { get; set; }

    public decimal? MatchConfidence { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime LastSeenAt { get; set; }
}