using System.Net;

namespace CartCompare.Infrastructure.Providers.Kroger;

public sealed class KrogerApiException : HttpRequestException
{
    public string Stage { get; }

    public KrogerApiException(string stage, HttpStatusCode statusCode)
        : base($"Kroger {stage} request failed.", null, statusCode)
    {
        Stage = stage;
    }
}
