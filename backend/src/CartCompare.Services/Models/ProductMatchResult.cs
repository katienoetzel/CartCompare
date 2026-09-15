using CartCompare.Entities.Enums;

namespace CartCompare.Services.Models;

public class ProductMatchResult
{
    public bool IsMatch { get; set; }

    public ProductMatchMethod? MatchMethod { get; set; }

    public decimal? MatchConfidence { get; set; }

    public required string Reason { get; set; }
}