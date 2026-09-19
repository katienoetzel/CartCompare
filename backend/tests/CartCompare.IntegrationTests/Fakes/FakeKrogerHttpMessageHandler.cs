using System.Net;
using System.Net.Http.Headers;

namespace CartCompare.IntegrationTests.Fakes;

public class FakeKrogerHttpMessageHandler
    : HttpMessageHandler
{
    public List<FakeKrogerHttpRequest>
        Requests
    { get; } =
            new();

    public Func<
        FakeKrogerHttpRequest,
        HttpResponseMessage
    >? ResponseFactory
    { get; set; }

    protected override async Task<HttpResponseMessage>
        SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
    {
        var content =
            request.Content is null
                ? null
                : await request.Content
                    .ReadAsStringAsync(
                        cancellationToken
                    );

        var capturedRequest =
            new FakeKrogerHttpRequest
            {
                Method =
                    request.Method,

                Uri =
                    request.RequestUri,

                PathAndQuery =
                    request.RequestUri
                        ?.PathAndQuery
                    ?? string.Empty,

                AuthorizationScheme =
                    request.Headers
                        .Authorization
                        ?.Scheme,

                AuthorizationParameter =
                    request.Headers
                        .Authorization
                        ?.Parameter,

                Content =
                    content
            };

        Requests.Add(
            capturedRequest
        );

        if (ResponseFactory is null)
        {
            throw new InvalidOperationException(
                "The fake Kroger HTTP handler "
                + "received a request but no "
                + "ResponseFactory was configured. "
                + $"Request: {request.Method} "
                + $"{request.RequestUri}"
            );
        }

        var response =
            ResponseFactory(
                capturedRequest
            );

        response.RequestMessage =
            request;

        return response;
    }

    public void Reset()
    {
        Requests.Clear();

        ResponseFactory =
            null;
    }
}

public class FakeKrogerHttpRequest
{
    public required HttpMethod Method
    {
        get;
        set;
    }

    public Uri? Uri
    {
        get;
        set;
    }

    public required string PathAndQuery
    {
        get;
        set;
    }

    public string? AuthorizationScheme
    {
        get;
        set;
    }

    public string? AuthorizationParameter
    {
        get;
        set;
    }

    public string? Content
    {
        get;
        set;
    }
}