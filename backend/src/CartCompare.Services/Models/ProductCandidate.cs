using CartCompare.Entities.Enums;

namespace CartCompare.Services.Models;

public class ProductCandidate
{
    public required string ExternalProductId { get; set; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Upc { get; set; }

    public bool IsMatch { get; set; }

    public ProductMatchMethod? MatchMethod { get; set; }

    public decimal? MatchConfidence { get; set; }

    public required string MatchReason { get; set; }
}