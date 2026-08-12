using CartCompare.Entities.Enums;

namespace CartCompare.Entities;

public class Price
{
    public int Id { get; set; }

    public int RetailerProductId { get; set; }

    public int StoreLocationId { get; set; }

    public decimal? RegularPrice { get; set; }

    public decimal? SalePrice { get; set; }

    public decimal? MemberPrice { get; set; }

    public AvailabilityStatus AvailabilityStatus { get; set; }

    public required string SourceProvider { get; set; }

    public DateTime? SourceUpdatedAt { get; set; }

    public DateTime LastCheckedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}