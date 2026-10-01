using System.Net;
using CartCompare.Infrastructure.Providers.LowesFoods;

namespace CartCompare.IntegrationTests.Providers;

public class LowesFoodsPostalCodeGeocoderTests
{
    [Fact]
    public async Task ResolveAsync_ValidZip_ReturnsCoordinates()
    {
        using var handler = new StubHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "post code": "27613",
                      "places": [
                        {
                          "latitude": "35.9049831",
                          "longitude": "-78.7088295"
                        }
                      ]
                    }
                    """
                )
            }
        );

        var geocoder = new LowesFoodsPostalCodeGeocoder(
            new StubClientFactory(handler)
        );

        var coordinates =
            await geocoder.ResolveAsync(" 27613 ");

        Assert.True(coordinates.HasValue);

        Assert.Equal(
            new LowesFoodsCoordinates(
                35.9049831m,
                -78.7088295m
            ),
            coordinates.Value
        );

        Assert.Equal(
            "https://api.zippopotam.us/us/27613",
            Assert.Single(handler.Requests)
        );
    }

    [Fact]
    public async Task ResolveAsync_UnknownOrInvalidZip_ReturnsNull()
    {
        using var handler = new StubHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        );

        var geocoder = new LowesFoodsPostalCodeGeocoder(
            new StubClientFactory(handler)
        );

        Assert.Null(
            await geocoder.ResolveAsync("not a ZIP")
        );

        Assert.Empty(handler.Requests);

        Assert.Null(
            await geocoder.ResolveAsync("99999")
        );

        Assert.Equal(
            "https://api.zippopotam.us/us/99999",
            Assert.Single(handler.Requests)
        );
    }

    private sealed class StubClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            Assert.Equal(
                LowesFoodsPostalCodeGeocoder.ClientName,
                name
            );

            return new HttpClient(
                _handler,
                disposeHandler: false
            )
            {
                BaseAddress =
                    new Uri("https://api.zippopotam.us/")
            };
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage>
            _responseFactory;

        public List<string> Requests { get; } = new();

        public StubHandler(
            Func<HttpRequestMessage, HttpResponseMessage>
                responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());

            return Task.FromResult(_responseFactory(request));
        }
    }
}
