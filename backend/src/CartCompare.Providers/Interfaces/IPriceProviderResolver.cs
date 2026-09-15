namespace CartCompare.Providers.Interfaces;

public interface IPriceProviderResolver
{
    IPriceProvider? Resolve(string retailerName);
}