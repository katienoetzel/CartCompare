using System.ComponentModel.DataAnnotations;

namespace CartCompare.Api.Models.Requests;

public class SetGroceryListQuantityRequest
{
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}