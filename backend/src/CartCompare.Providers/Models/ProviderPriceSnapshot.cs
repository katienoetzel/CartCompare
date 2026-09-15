using CartCompare.Entities.Enums;

namespace CartCompare.Providers.Models;

public class ProviderPriceSnapshot
{
    public decimal? RegularPrice { get; set; }

    public decimal? SalePrice { get; set; }

    public decimal? MemberPrice { get; set; }

    public AvailabilityStatus AvailabilityStatus { get; set; }

    public DateTime? SourceUpdatedAt { get; set; }
}