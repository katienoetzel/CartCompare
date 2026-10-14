using System.ComponentModel.DataAnnotations;

namespace CartCompare.Api.Models.Requests;

public class CompareStoresRequest
{
    [MinLength(1)]
    [MaxLength(10)]
    public required List<int> StoreLocationIds { get; set; }
}