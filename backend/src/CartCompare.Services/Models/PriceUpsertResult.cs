using CartCompare.Entities;

namespace CartCompare.Services.Models;

public class PriceUpsertResult
{
    public PriceUpsertResultType Result { get; set; }

    public Price? StoredPrice { get; set; }
}