using CartCompare.Entities.Enums;

namespace CartCompare.Services.Models;

public class EffectivePriceResult
{
    public decimal? Amount { get; set; }

    public EffectivePriceType? PriceType { get; set; }

    public AvailabilityStatus AvailabilityStatus { get; set; }

    public bool HasRetailerMembership { get; set; }

    public DateTime LastCheckedAt { get; set; }

    public string? SourceProvider { get; set; }
}