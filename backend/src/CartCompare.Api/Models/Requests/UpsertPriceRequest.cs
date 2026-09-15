using CartCompare.Entities.Enums;

namespace CartCompare.Api.Models.Requests;

public class UpsertPriceRequest
{
    public int RetailerProductId { get; set; }

    public int StoreLocationId { get; set; }

    public decimal? RegularPrice { get; set; }

    public decimal? SalePrice { get; set; }

    public decimal? MemberPrice { get; set; }

    public AvailabilityStatus AvailabilityStatus { get; set; }

    public required string SourceProvider { get; set; }

    public DateTime? SourceUpdatedAt { get; set; }
}