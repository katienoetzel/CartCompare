using System.ComponentModel.DataAnnotations;

namespace CartCompare.Api.Models.Requests;

public class CreateCatalogItemRequest
{
    [Range(1, int.MaxValue)]
    public int StoreLocationId { get; set; }

    [Required]
    [StringLength(100)]
    public required string ExternalProductId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public required string Name { get; set; }

    [Required]
    [StringLength(50)]
    public required string Size { get; set; }

    [StringLength(50)]
    public string? Category { get; set; }
}
