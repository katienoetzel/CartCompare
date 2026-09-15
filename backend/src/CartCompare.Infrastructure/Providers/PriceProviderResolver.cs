using CartCompare.Providers.Interfaces;

namespace CartCompare.Infrastructure.Providers;

public class PriceProviderResolver : IPriceProviderResolver
{
    private readonly IEnumerable<IPriceProvider> _providers;

    public PriceProviderResolver(
        IEnumerable<IPriceProvider> providers)
    {
        _providers = providers;
    }

    public IPriceProvider? Resolve(string retailerName)
    {
        return _providers.FirstOrDefault(
            provider =>
                provider.SupportsRetailer(retailerName)
        );
    }
}