using System.Text.Json;
using CartCompare.Infrastructure.Providers.LowesFoods;

namespace CartCompare.IntegrationTests.Providers;

public class LowesFoodsLocationParsingTests
{
    [Fact]
    public void ExtractLocations_KeepsNearbyStoresAcrossZipCodes()
    {
        using var response = JsonDocument.Parse(
            """
            {
              "data": {
                "item": {
                  "modules": {
                    "itemListModules": [{
                      "items": {
                        "locationItems": [
                          {
                            "locationId": "BRIER-CREEK",
                            "locationName": "Lowes Foods Brier Creek",
                            "locationAddress": {
                              "address1": "8100 Brier Creek Parkway",
                              "city": "Raleigh",
                              "state": "NC",
                              "postalCode": "27617"
                            },
                            "distanceUnits": "mi",
                            "distanceFromSearchCoordinates": 4.3
                          },
                          {
                            "locationId": "STRICKLAND",
                            "locationName": "Lowes Foods Strickland",
                            "locationAddress": {
                              "address1": "9600 Strickland Road",
                              "city": "Raleigh",
                              "state": "NC",
                              "postalCode": "27615"
                            },
                            "distanceUnits": "mi",
                            "distanceFromSearchCoordinates": 3.1
                          },
                          {
                            "locationId": "FAR-AWAY",
                            "locationName": "Lowes Foods Far Away",
                            "locationAddress": {
                              "address1": "100 Main Street",
                              "city": "Charleston",
                              "state": "SC",
                              "postalCode": "29414"
                            },
                            "distanceUnits": "mi",
                            "distanceFromSearchCoordinates": 187.5
                          },
                          {
                            "locationId": "UNKNOWN-DISTANCE",
                            "locationName": "Lowes Foods Unknown",
                            "locationAddress": {
                              "address1": "200 Main Street",
                              "city": "Raleigh",
                              "state": "NC",
                              "postalCode": "27613"
                            }
                          }
                        ]
                      }
                    }]
                  }
                }
              }
            }
            """
        );

        var stores =
            LowesFoodsPriceProvider.ExtractLocations(
                response.RootElement
            );

        Assert.Equal(2, stores.Count);
        Assert.Equal("STRICKLAND", stores[0].ExternalLocationId);
        Assert.Equal("27615", stores[0].PostalCode);
        Assert.Equal("BRIER-CREEK", stores[1].ExternalLocationId);
        Assert.Equal("27617", stores[1].PostalCode);
    }
}
