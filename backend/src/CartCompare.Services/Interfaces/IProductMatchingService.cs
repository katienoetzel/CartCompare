using CartCompare.Entities;
using CartCompare.Providers.Models;
using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IProductMatchingService
{
    Task<ProductMatchResult> EvaluateAsync(
        Item item,
        ProviderProduct providerProduct
    );

    // The name, brand and size rules without the UPC lookup.
    ProductMatchResult EvaluateDescription(
        Item item,
        ProviderProduct providerProduct
    );
}