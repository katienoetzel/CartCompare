using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

using CartCompare.Entities.Enums;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;

namespace CartCompare.Infrastructure.Providers.LowesFoods;

public sealed class LowesFoodsPriceProvider
    : IPriceProvider
{
    private const decimal MaximumStoreDistanceMiles = 25m;
    private const int MaximumStoreResults = 20;

    private readonly LowesFoodsSessionClient
        _sessionClient;

    private readonly LowesFoodsPostalCodeGeocoder
        _postalCodeGeocoder;

    private readonly ConcurrentDictionary<
        string,
        LowesFoodsProductRecord
    >
        _productCache =
            new(
                StringComparer
                    .OrdinalIgnoreCase
            );

    public LowesFoodsPriceProvider(
        LowesFoodsSessionClient sessionClient,
        LowesFoodsPostalCodeGeocoder postalCodeGeocoder)
    {
        _sessionClient =
            sessionClient;

        _postalCodeGeocoder =
            postalCodeGeocoder;
    }

    public string ProviderName =>
        "LowesFoodsInmar";

    public bool SupportsRetailer(
        string retailerName)
    {
        return string.Equals(
            retailerName?.Trim(),
            "Lowes Foods",
            StringComparison
                .OrdinalIgnoreCase
        );
    }

    public async Task<
        List<ProviderStoreLocation>
    >
        FindStoresAsync(
            string retailerName,
            string postalCode)
    {
        if (
            !SupportsRetailer(
                retailerName
            )
            ||
            string.IsNullOrWhiteSpace(
                postalCode
            )
        )
        {
            return new List<
                ProviderStoreLocation
            >();
        }

        var coordinates =
            await _postalCodeGeocoder
                .ResolveAsync(postalCode);

        if (coordinates is null)
        {
            return new List<ProviderStoreLocation>();
        }

        using var document =
            await _sessionClient.GetLocationsAsync(
                coordinates.Value.Latitude,
                coordinates.Value.Longitude
            );

        // Inmar searches around these coordinates. The
        // nearby stores can have different postal codes.
        return ExtractLocations(document.RootElement);
    }

    public async Task<
        List<ProviderProduct>
    >
        SearchProductsAsync(
            string retailerName,
            string externalLocationId,
            string query)
    {
        if (
            !SupportsRetailer(
                retailerName
            )
            ||
            string.IsNullOrWhiteSpace(
                externalLocationId
            )
            ||
            string.IsNullOrWhiteSpace(
                query
            )
        )
        {
            return new List<
                ProviderProduct
            >();
        }

        var records =
            await SearchRecordsAsync(
                externalLocationId,
                query
            );

        foreach (
            var record in records
        )
        {
            _productCache[
                record.ExternalProductId
            ] =
                record;
        }

        return records
            .Select(
                ToProviderProduct
            )
            .ToList();
    }

    public async Task<ProviderProduct?>
        GetProductAsync(
            string retailerName,
            string externalLocationId,
            string externalProductId)
    {
        if (
            !SupportsRetailer(
                retailerName
            )
            ||
            string.IsNullOrWhiteSpace(
                externalLocationId
            )
            ||
            string.IsNullOrWhiteSpace(
                externalProductId
            )
        )
        {
            return null;
        }

        var record =
            await FindProductRecordAsync(
                externalLocationId,
                externalProductId
            );

        return record is null
            ? null
            : ToProviderProduct(
                record
            );
    }

    public async Task<
        ProviderPriceSnapshot?
    >
        GetPriceAsync(
            string retailerName,
            string externalLocationId,
            string externalProductId)
    {
        if (
            !SupportsRetailer(
                retailerName
            )
            ||
            string.IsNullOrWhiteSpace(
                externalLocationId
            )
            ||
            string.IsNullOrWhiteSpace(
                externalProductId
            )
        )
        {
            return null;
        }

        var record =
            await FindProductRecordAsync(
                externalLocationId,
                externalProductId
            );

        if (record is null)
        {
            return null;
        }

        return new ProviderPriceSnapshot
        {
            RegularPrice =
                record.Price,

            SalePrice =
                null,

            MemberPrice =
                null,

            AvailabilityStatus =
                record.AvailabilityStatus,

            SourceUpdatedAt =
                null
        };
    }

    private async Task<
        LowesFoodsProductRecord?
    >
        FindProductRecordAsync(
            string externalLocationId,
            string externalProductId)
    {
        var trimmedProductId =
            externalProductId.Trim();

        if (
            _productCache.TryGetValue(
                trimmedProductId,
                out var cached
            )
        )
        {
            var refreshed =
                await SearchRecordsAsync(
                    externalLocationId,
                    cached.Name
                );

            var exactRefreshed =
                refreshed
                    .FirstOrDefault(
                        product =>
                            string.Equals(
                                product
                                    .ExternalProductId,
                                trimmedProductId,
                                StringComparison
                                    .OrdinalIgnoreCase
                            )
                    );

            if (
                exactRefreshed
                is not null
            )
            {
                _productCache[
                    trimmedProductId
                ] =
                    exactRefreshed;

                return exactRefreshed;
            }

            return cached;
        }

        // Fallback for a provider product ID that was not
        // discovered during this application process.
        // Inmar search may match the external product ID
        // directly. If it does not, callers receive null
        // rather than an incorrect product.
        var results =
            await SearchRecordsAsync(
                externalLocationId,
                trimmedProductId
            );

        var exact =
            results
                .FirstOrDefault(
                    product =>
                        string.Equals(
                            product
                                .ExternalProductId,
                            trimmedProductId,
                            StringComparison
                                .OrdinalIgnoreCase
                        )
                );

        if (exact is not null)
        {
            _productCache[
                trimmedProductId
            ] =
                exact;
        }

        return exact;
    }

    private async Task<
        List<LowesFoodsProductRecord>
    >
        SearchRecordsAsync(
            string externalLocationId,
            string query)
    {
        using var document =
            await _sessionClient
                .SearchAsync(
                    externalLocationId,
                    query
                );

        return ExtractProducts(
            document.RootElement
        );
    }

    private static List<
        LowesFoodsProductRecord
    >
        ExtractProducts(
            JsonElement root)
    {
        var records =
            new Dictionary<
                string,
                LowesFoodsProductRecord
            >(
                StringComparer
                    .OrdinalIgnoreCase
            );

        foreach (
            var element
            in Walk(root)
        )
        {
            if (
                element.ValueKind
                !=
                JsonValueKind.Object
            )
            {
                continue;
            }

            if (
                !TryGetString(
                    element,
                    "productId",
                    out var productId
                )
                ||
                !TryGetString(
                    element,
                    "name",
                    out var name
                )
            )
            {
                continue;
            }

            var priceInCents =
                TryGetDecimal(
                    element,
                    "chargePrice"
                )
                ??
                TryGetDecimal(
                    element,
                    "orderPrice"
                );

            var price =
                priceInCents.HasValue
                    ? decimal.Round(
                        priceInCents.Value
                        / 100m,
                        2
                    )
                    : (decimal?)null;

            var size =
                BuildSize(
                    element
                );

            var availabilityStatus =
                GetAvailabilityStatus(
                    element
                );

            records[
                productId
            ] =
                new LowesFoodsProductRecord
                {
                    ExternalProductId =
                        productId,

                    Name =
                        name,

                    Brand =
                        null,

                    Size =
                        size,

                    Upc =
                        GetOptionalString(
                            element,
                            "upc"
                        ),

                    Price =
                        price,

                    AvailabilityStatus =
                        availabilityStatus
                };
        }

        return records.Values
            .ToList();
    }

    internal static List<
        ProviderStoreLocation
    >
        ExtractLocations(
            JsonElement root)
    {
        var stores =
            new Dictionary<
                string,
                (ProviderStoreLocation Store, decimal DistanceMiles)
            >(
                StringComparer
                    .OrdinalIgnoreCase
            );

        foreach (
            var element
            in Walk(root)
        )
        {
            if (
                element.ValueKind
                !=
                JsonValueKind.Object
            )
            {
                continue;
            }

            if (
                !TryGetString(
                    element,
                    "locationId",
                    out var locationId
                )
            )
            {
                continue;
            }

            var distance = TryGetDecimal(
                element,
                "distanceFromSearchCoordinates"
            );

            var distanceUnits = GetOptionalString(
                element,
                "distanceUnits"
            );

            if (distance is null
                || distance < 0
                || distance > MaximumStoreDistanceMiles
                || !string.Equals(
                    distanceUnits,
                    "mi",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                continue;
            }

            var address =
                GetObject(
                    element,
                    "locationAddress"
                )
                ??
                GetObject(
                    element,
                    "address"
                );

            if (address is null)
            {
                continue;
            }

            var addressLine1 =
                GetOptionalString(
                    address.Value,
                    "address1"
                )
                ??
                GetOptionalString(
                    address.Value,
                    "addressLine1"
                );

            var addressLine2 =
                GetOptionalString(
                    address.Value,
                    "address2"
                )
                ??
                GetOptionalString(
                    address.Value,
                    "addressLine2"
                );

            var city =
                GetOptionalString(
                    address.Value,
                    "city"
                );

            var state =
                GetOptionalString(
                    address.Value,
                    "state"
                );

            var postalCode =
                GetOptionalString(
                    address.Value,
                    "postalCode"
                )
                ??
                GetOptionalString(
                    address.Value,
                    "zipCode"
                )
                ??
                GetOptionalString(
                    address.Value,
                    "zip"
                );

            if (
                string.IsNullOrWhiteSpace(
                    addressLine1
                )
                ||
                string.IsNullOrWhiteSpace(
                    city
                )
                ||
                string.IsNullOrWhiteSpace(
                    state
                )
                ||
                string.IsNullOrWhiteSpace(
                    postalCode
                )
            )
            {
                continue;
            }

            var name =
                GetOptionalString(
                    element,
                    "locationName"
                )
                ??
                GetOptionalString(
                    element,
                    "name"
                )
                ??
                GetOptionalString(
                    address.Value,
                    "name"
                )
                ??
                "Lowes Foods";

            var latitude =
                TryGetDecimal(
                    element,
                    "latitude"
                )
                ??
                TryGetDecimal(
                    address.Value,
                    "latitude"
                );

            var longitude =
                TryGetDecimal(
                    element,
                    "longitude"
                )
                ??
                TryGetDecimal(
                    element,
                    "long"
                )
                ??
                TryGetDecimal(
                    address.Value,
                    "longitude"
                )
                ??
                TryGetDecimal(
                    address.Value,
                    "long"
                );

            stores[locationId] = (
                new ProviderStoreLocation
                {
                    ExternalLocationId =
                        locationId,

                    Name =
                        name,

                    AddressLine1 =
                        addressLine1,

                    AddressLine2 =
                        addressLine2,

                    City =
                        city,

                    State =
                        state,

                    PostalCode =
                        postalCode,

                    Latitude =
                        latitude,

                    Longitude =
                        longitude
                },
                distance.Value
            );
        }

        return stores.Values
            .OrderBy(location => location.DistanceMiles)
            .ThenBy(location => location.Store.Name)
            .Take(MaximumStoreResults)
            .Select(location => location.Store)
            .ToList();
    }

    private static ProviderProduct
        ToProviderProduct(
            LowesFoodsProductRecord record)
    {
        return new ProviderProduct
        {
            ExternalProductId =
                record.ExternalProductId,

            Name =
                record.Name,

            Brand =
                record.Brand,

            Size =
                record.Size,

            Upc =
                record.Upc
        };
    }

    private static AvailabilityStatus
        GetAvailabilityStatus(
            JsonElement product)
    {
        if (
            product.TryGetProperty(
                "isAvailableForLocation",
                out var availableElement
            )
            &&
            (
                availableElement.ValueKind
                ==
                JsonValueKind.True
                ||
                availableElement.ValueKind
                ==
                JsonValueKind.False
            )
        )
        {
            return availableElement
                .GetBoolean()
                    ? AvailabilityStatus
                        .Available
                    : AvailabilityStatus
                        .Unavailable;
        }

        var inventoryStatus =
            GetOptionalString(
                product,
                "inventoryStatus"
            );

        if (
            string.Equals(
                inventoryStatus,
                "active",
                StringComparison
                    .OrdinalIgnoreCase
            )
        )
        {
            return AvailabilityStatus
                .Available;
        }

        if (
            string.Equals(
                inventoryStatus,
                "inactive",
                StringComparison
                    .OrdinalIgnoreCase
            )
        )
        {
            return AvailabilityStatus
                .Unavailable;
        }

        return AvailabilityStatus
            .Unknown;
    }

    private static string?
        BuildSize(
            JsonElement product)
    {
        var packageSize =
            TryGetDecimal(
                product,
                "packageSize"
            );

        var packageUnit =
            GetOptionalString(
                product,
                "packageSizeUOM"
            );

        if (
            packageSize.HasValue
            &&
            !string.IsNullOrWhiteSpace(
                packageUnit
            )
        )
        {
            return
                $"{packageSize.Value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture
                )} {packageUnit}";
        }

        return null;
    }

    private static IEnumerable<
        JsonElement
    >
        Walk(
            JsonElement element)
    {
        yield return element;

        if (
            element.ValueKind
            ==
            JsonValueKind.Object
        )
        {
            foreach (
                var property
                in element
                    .EnumerateObject()
            )
            {
                foreach (
                    var child
                    in Walk(
                        property.Value
                    )
                )
                {
                    yield return child;
                }
            }
        }
        else if (
            element.ValueKind
            ==
            JsonValueKind.Array
        )
        {
            foreach (
                var item
                in element
                    .EnumerateArray()
            )
            {
                foreach (
                    var child
                    in Walk(item)
                )
                {
                    yield return child;
                }
            }
        }
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value =
            string.Empty;

        if (
            !element.TryGetProperty(
                propertyName,
                out var property
            )
            ||
            property.ValueKind
            !=
            JsonValueKind.String
        )
        {
            return false;
        }

        var result =
            property.GetString();

        if (
            string.IsNullOrWhiteSpace(
                result
            )
        )
        {
            return false;
        }

        value =
            result.Trim();

        return true;
    }

    private static string?
        GetOptionalString(
            JsonElement element,
            string propertyName)
    {
        return TryGetString(
            element,
            propertyName,
            out var value
        )
            ? value
            : null;
    }

    private static decimal?
        TryGetDecimal(
            JsonElement element,
            string propertyName)
    {
        if (
            !element.TryGetProperty(
                propertyName,
                out var property
            )
        )
        {
            return null;
        }

        if (
            property.ValueKind
            ==
            JsonValueKind.Number
            &&
            property.TryGetDecimal(
                out var numeric
            )
        )
        {
            return numeric;
        }

        if (
            property.ValueKind
            ==
            JsonValueKind.String
            &&
            decimal.TryParse(
                property.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed
            )
        )
        {
            return parsed;
        }

        return null;
    }

    private static JsonElement?
        GetObject(
            JsonElement element,
            string propertyName)
    {
        if (
            element.TryGetProperty(
                propertyName,
                out var property
            )
            &&
            property.ValueKind
            ==
            JsonValueKind.Object
        )
        {
            return property;
        }

        return null;
    }

    private sealed class
        LowesFoodsProductRecord
    {
        public required string
            ExternalProductId
            { get; init; }

        public required string Name
            { get; init; }

        public string? Brand
            { get; init; }

        public string? Size
            { get; init; }

        public string? Upc
            { get; init; }

        public decimal? Price
            { get; init; }

        public AvailabilityStatus
            AvailabilityStatus
            { get; init; }
    }
}
