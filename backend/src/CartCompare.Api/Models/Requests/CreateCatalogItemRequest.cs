using System.ComponentModel.DataAnnotations;
using CartCompare.Services.Models;

namespace CartCompare.Api.Models.Requests;

// Only which product and which kind of item: the server builds the
// name, brand and size from the retailer's product.
public class CreateCatalogItemRequest
{
    [Range(1, int.MaxValue)]
    public int StoreLocationId { get; set; }

    [Required]
    [StringLength(100)]
    public required string ExternalProductId { get; set; }

    [EnumDataType(typeof(CatalogItemKind))]
    public CatalogItemKind Kind { get; set; }
}
