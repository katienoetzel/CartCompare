using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CartCompare.Entities.Enums;
using CartCompare.IntegrationTests.Fakes;
using CartCompare.IntegrationTests.Fixtures;
using CartCompare.Providers.Models;

namespace CartCompare.IntegrationTests.Api;

public class IntegrationsApiTests
    : PostgresIntegrationTestBase
{
    [Theory]
    [InlineData(
        "/api/integrations/kroger/status"
    )]
    [InlineData(
        "/api/integrations/kroger/stores?postalCode=27606"
    )]
    [InlineData(
        "/api/integrations/kroger/products?locationId=STORE-101&query=milk"
    )]
    [InlineData(
        "/api/integrations/kroger/price?locationId=STORE-101&productId=PRODUCT-1"
    )]
    public async Task KrogerEndpoints_WithoutToken_ReturnUnauthorized(
        string url)
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                url
            );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );

        Assert.Empty(
            factory.TestKrogerHttpHandler
                .Requests
        );
    }

    [Fact]
    public async Task KrogerStatus_WithToken_RequestsOAuthTokenAndReturnsConnected()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        ConfigureTokenOnly(
            factory
        );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "kroger-status@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/integrations/kroger/status"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.True(
            document.RootElement
                .GetProperty("connected")
                .GetBoolean()
        );

        Assert.Equal(
            "Kroger",
            document.RootElement
                .GetProperty("provider")
                .GetString()
        );

        Assert.Single(
            factory.TestKrogerHttpHandler
                .Requests
        );

        var request =
            factory.TestKrogerHttpHandler
                .Requests
                .Single();

        Assert.Equal(
            HttpMethod.Post,
            request.Method
        );

        Assert.Equal(
            "/v1/connect/oauth2/token",
            request.PathAndQuery
        );

        Assert.Equal(
            "Basic",
            request.AuthorizationScheme
        );

        Assert.False(
            string.IsNullOrWhiteSpace(
                request.AuthorizationParameter
            )
        );

        Assert.NotNull(
            request.Content
        );

        Assert.Contains(
            "grant_type=client_credentials",
            request.Content!
        );

        Assert.Contains(
            "scope=product.compact",
            request.Content!
        );
    }

    [Fact]
    public async Task KrogerStores_WithBlankPostalCode_ReturnsBadRequestWithoutKrogerCall()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "blank-kroger-postal@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var response =
            await client.GetAsync(
                "/api/integrations/kroger/stores"
                + "?postalCode=%20"
            );

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "Postal code is required.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestKrogerHttpHandler
                .Requests
        );
    }

    [Fact]
    public async Task KrogerStores_WithValidPostalCode_ReturnsMappedStores()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestKrogerHttpHandler
            .ResponseFactory =
                request =>
                {
                    if (IsTokenRequest(request))
                    {
                        return TokenResponse();
                    }

                    if (
                        request.Method ==
                            HttpMethod.Get
                        &&
                        request.PathAndQuery
                            .StartsWith(
                                "/v1/locations?",
                                StringComparison.Ordinal
                            )
                    )
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            """
                            {
                              "data": [
                                {
                                  "locationId": "STORE-101",
                                  "chain": "Kroger",
                                  "name": "Kroger Raleigh",
                                  "address": {
                                    "addressLine1": "123 Test Street",
                                    "addressLine2": "Suite 5",
                                    "city": "Raleigh",
                                    "state": "NC",
                                    "zipCode": "27606"
                                  },
                                  "geolocation": {
                                    "latitude": 35.780,
                                    "longitude": -78.640
                                  }
                                },
                                {
                                  "locationId": "",
                                  "chain": "Kroger",
                                  "name": "Invalid Store",
                                  "address": {
                                    "addressLine1": "Bad",
                                    "city": "Raleigh",
                                    "state": "NC",
                                    "zipCode": "27606"
                                  }
                                }
                              ]
                            }
                            """
                        );
                    }

                    throw UnexpectedRequest(
                        request
                    );
                };

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "kroger-stores@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/integrations/kroger/stores"
                + "?postalCode=27606"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var stores =
            await response.Content
                .ReadFromJsonAsync<
                    List<ProviderStoreLocation>
                >();

        Assert.NotNull(
            stores
        );

        Assert.Single(
            stores!
        );

        var store =
            stores[0];

        Assert.Equal(
            "STORE-101",
            store.ExternalLocationId
        );

        Assert.Equal(
            "Kroger Raleigh",
            store.Name
        );

        Assert.Equal(
            "123 Test Street",
            store.AddressLine1
        );

        Assert.Equal(
            "Suite 5",
            store.AddressLine2
        );

        Assert.Equal(
            "Raleigh",
            store.City
        );

        Assert.Equal(
            "NC",
            store.State
        );

        Assert.Equal(
            "27606",
            store.PostalCode
        );

        Assert.Equal(
            35.780m,
            store.Latitude
        );

        Assert.Equal(
            -78.640m,
            store.Longitude
        );

        Assert.Equal(
            2,
            factory.TestKrogerHttpHandler
                .Requests
                .Count
        );

        var krogerRequest =
            factory.TestKrogerHttpHandler
                .Requests[1];

        Assert.Equal(
            HttpMethod.Get,
            krogerRequest.Method
        );

        Assert.StartsWith(
            "/v1/locations?",
            krogerRequest.PathAndQuery
        );

        Assert.Contains(
            "filter.zipCode.near=27606",
            krogerRequest.PathAndQuery
        );

        Assert.Contains(
            "filter.limit=50",
            krogerRequest.PathAndQuery
        );

        Assert.Contains(
            "filter.chain=Kroger",
            krogerRequest.PathAndQuery
        );

        Assert.Equal(
            "Bearer",
            krogerRequest.AuthorizationScheme
        );

        Assert.Equal(
            "fake-kroger-token",
            krogerRequest.AuthorizationParameter
        );
    }

    [Fact]
    public async Task KrogerProducts_WithMissingArguments_ReturnsBadRequestWithoutKrogerCall()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "invalid-kroger-products@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var response =
            await client.GetAsync(
                "/api/integrations/kroger/products"
                + "?locationId="
                + "&query="
            );

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "Location ID and search query are required.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestKrogerHttpHandler
                .Requests
        );
    }

    [Fact]
    public async Task KrogerProducts_WithValidArguments_ReturnsMappedProducts()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestKrogerHttpHandler
            .ResponseFactory =
                request =>
                {
                    if (IsTokenRequest(request))
                    {
                        return TokenResponse();
                    }

                    if (
                        request.Method ==
                            HttpMethod.Get
                        &&
                        request.PathAndQuery
                            .StartsWith(
                                "/v1/products?",
                                StringComparison.Ordinal
                            )
                    )
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            """
                            {
                              "data": [
                                {
                                  "productId": "PRODUCT-1",
                                  "brand": "Test Brand",
                                  "description": "Whole Milk",
                                  "upc": "012345678905",
                                  "items": [
                                    {
                                      "itemId": "ITEM-1",
                                      "size": "1 gallon"
                                    }
                                  ]
                                },
                                {
                                  "productId": "",
                                  "description": ""
                                }
                              ]
                            }
                            """
                        );
                    }

                    throw UnexpectedRequest(
                        request
                    );
                };

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "kroger-products@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/integrations/kroger/products"
                + "?locationId=STORE-101"
                + "&query=whole%20milk"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var products =
            await response.Content
                .ReadFromJsonAsync<
                    List<ProviderProduct>
                >();

        Assert.NotNull(
            products
        );

        Assert.Single(
            products!
        );

        var product =
            products[0];

        Assert.Equal(
            "PRODUCT-1",
            product.ExternalProductId
        );

        Assert.Equal(
            "Whole Milk",
            product.Name
        );

        Assert.Equal(
            "Test Brand",
            product.Brand
        );

        Assert.Equal(
            "1 gallon",
            product.Size
        );

        Assert.Equal(
            "012345678905",
            product.Upc
        );

        Assert.Equal(
            2,
            factory.TestKrogerHttpHandler
                .Requests
                .Count
        );

        var krogerRequest =
            factory.TestKrogerHttpHandler
                .Requests[1];

        Assert.StartsWith(
            "/v1/products?",
            krogerRequest.PathAndQuery
        );

        Assert.Contains(
            "filter.term=whole%20milk",
            krogerRequest.PathAndQuery
        );

        Assert.Contains(
            "filter.locationId=STORE-101",
            krogerRequest.PathAndQuery
        );

        Assert.Contains(
            "filter.limit=25",
            krogerRequest.PathAndQuery
        );

        Assert.Equal(
            "Bearer",
            krogerRequest.AuthorizationScheme
        );

        Assert.Equal(
            "fake-kroger-token",
            krogerRequest.AuthorizationParameter
        );
    }

    [Fact]
    public async Task KrogerPrice_WithMissingArguments_ReturnsBadRequestWithoutKrogerCall()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "invalid-kroger-price@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        var response =
            await client.GetAsync(
                "/api/integrations/kroger/price"
                + "?locationId="
                + "&productId="
            );

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        Assert.Equal(
            "Location ID and product ID are required.",
            document.RootElement
                .GetProperty("message")
                .GetString()
        );

        Assert.Empty(
            factory.TestKrogerHttpHandler
                .Requests
        );
    }

    [Fact]
    public async Task KrogerPrice_WhenProductDoesNotExist_ReturnsNotFound()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestKrogerHttpHandler
            .ResponseFactory =
                request =>
                {
                    if (IsTokenRequest(request))
                    {
                        return TokenResponse();
                    }

                    if (
                        request.Method ==
                            HttpMethod.Get
                        &&
                        request.PathAndQuery
                            .StartsWith(
                                "/v1/products/PRODUCT-404?",
                                StringComparison.Ordinal
                            )
                    )
                    {
                        return new HttpResponseMessage(
                            HttpStatusCode.NotFound
                        );
                    }

                    throw UnexpectedRequest(
                        request
                    );
                };

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "missing-kroger-price@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/integrations/kroger/price"
                + "?locationId=STORE-101"
                + "&productId=PRODUCT-404"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );

        Assert.Equal(
            2,
            factory.TestKrogerHttpHandler
                .Requests
                .Count
        );

        var request =
            factory.TestKrogerHttpHandler
                .Requests[1];

        Assert.Contains(
            "/v1/products/PRODUCT-404",
            request.PathAndQuery
        );

        Assert.Contains(
            "filter.locationId=STORE-101",
            request.PathAndQuery
        );
    }

    [Fact]
    public async Task KrogerPrice_WithValidProduct_ReturnsMappedPriceSnapshot()
    {
        using var factory =
            new CartCompareWebApplicationFactory(
                ConnectionString
            );

        factory.TestKrogerHttpHandler
            .ResponseFactory =
                request =>
                {
                    if (IsTokenRequest(request))
                    {
                        return TokenResponse();
                    }

                    if (
                        request.Method ==
                            HttpMethod.Get
                        &&
                        request.PathAndQuery
                            .StartsWith(
                                "/v1/products/PRODUCT-1?",
                                StringComparison.Ordinal
                            )
                    )
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            """
                            {
                              "data": {
                                "productId": "PRODUCT-1",
                                "brand": "Test Brand",
                                "description": "Whole Milk",
                                "upc": "012345678905",
                                "items": [
                                  {
                                    "itemId": "ITEM-1",
                                    "size": "1 gallon",
                                    "inventory": {
                                      "stockLevel": "IN_STOCK"
                                    },
                                    "price": {
                                      "regular": 5.49,
                                      "promo": 4.49
                                    }
                                  }
                                ]
                              }
                            }
                            """
                        );
                    }

                    throw UnexpectedRequest(
                        request
                    );
                };

        using var client =
            factory.CreateClient();

        var token =
            await RegisterAndGetTokenAsync(
                client,
                "successful-kroger-price@example.com"
            );

        SetBearerToken(
            client,
            token
        );

        // Act
        var response =
            await client.GetAsync(
                "/api/integrations/kroger/price"
                + "?locationId=STORE-101"
                + "&productId=PRODUCT-1"
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var price =
            await response.Content
                .ReadFromJsonAsync<
                    ProviderPriceSnapshot
                >();

        Assert.NotNull(
            price
        );

        Assert.Equal(
            5.49m,
            price!.RegularPrice
        );

        Assert.Equal(
            4.49m,
            price.SalePrice
        );

        // KrogerPriceProvider currently does not
        // populate a separate membership price.
        Assert.Null(
            price.MemberPrice
        );

        Assert.Equal(
            AvailabilityStatus.Available,
            price.AvailabilityStatus
        );

        Assert.Null(
            price.SourceUpdatedAt
        );

        Assert.Equal(
            2,
            factory.TestKrogerHttpHandler
                .Requests
                .Count
        );

        var krogerRequest =
            factory.TestKrogerHttpHandler
                .Requests[1];

        Assert.Equal(
            HttpMethod.Get,
            krogerRequest.Method
        );

        Assert.Contains(
            "/v1/products/PRODUCT-1",
            krogerRequest.PathAndQuery
        );

        Assert.Contains(
            "filter.locationId=STORE-101",
            krogerRequest.PathAndQuery
        );

        Assert.Equal(
            "Bearer",
            krogerRequest.AuthorizationScheme
        );

        Assert.Equal(
            "fake-kroger-token",
            krogerRequest.AuthorizationParameter
        );
    }

    // =================================================
    // FAKE KROGER RESPONSE HELPERS
    // =================================================

    private static void ConfigureTokenOnly(
        CartCompareWebApplicationFactory factory)
    {
        factory.TestKrogerHttpHandler
            .ResponseFactory =
                request =>
                {
                    if (IsTokenRequest(request))
                    {
                        return TokenResponse();
                    }

                    throw UnexpectedRequest(
                        request
                    );
                };
    }

    private static bool IsTokenRequest(
        FakeKrogerHttpRequest request)
    {
        return
            request.Method ==
                HttpMethod.Post
            &&
            request.PathAndQuery ==
                "/v1/connect/oauth2/token";
    }

    private static HttpResponseMessage
        TokenResponse()
    {
        return JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "access_token": "fake-kroger-token",
              "expires_in": 3600,
              "token_type": "bearer"
            }
            """
        );
    }

    private static HttpResponseMessage
        JsonResponse(
            HttpStatusCode statusCode,
            string json)
    {
        return new HttpResponseMessage(
            statusCode
        )
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                )
        };
    }

    private static Exception
        UnexpectedRequest(
            FakeKrogerHttpRequest request)
    {
        return new InvalidOperationException(
            "Unexpected fake Kroger request: "
            + $"{request.Method} "
            + request.PathAndQuery
        );
    }

    // =================================================
    // AUTHENTICATION HELPERS
    // =================================================

    private static async Task<string>
        RegisterAndGetTokenAsync(
            HttpClient client,
            string email)
    {
        var request =
            new
            {
                FirstName =
                    "Katie",

                LastName =
                    "Tester",

                Email =
                    email,

                Password =
                    "CartCompare123!",

                DefaultPostalCode =
                    "27606"
            };

        var response =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                request
            );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var json =
            await response.Content
                .ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        var token =
            document.RootElement
                .GetProperty("token")
                .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(
                token
            )
        );

        return token!;
    }

    private static void SetBearerToken(
        HttpClient client,
        string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token
            );
    }
}