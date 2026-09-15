using CartCompare.Entities;

namespace CartCompare.Services.Models;

public class CreateRetailerProductResult
{
    public RetailerProductCreateResult Result { get; set; }

    public RetailerProduct? RetailerProduct { get; set; }
}