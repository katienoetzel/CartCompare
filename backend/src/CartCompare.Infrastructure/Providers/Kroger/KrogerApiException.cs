using System.Net;
using System.Text.RegularExpressions;

namespace CartCompare.Infrastructure.Providers.Kroger;

public sealed class KrogerApiException : HttpRequestException
{
    // Enough of Kroger's reply to tell an API error (JSON) from a
    // firewall block (an "Access Denied" HTML page with a reference).
    private const int MaxSnippetLength = 300;

    public string Stage { get; }

    public string? ResponseSnippet { get; }

    public KrogerApiException(
        string stage,
        HttpStatusCode statusCode,
        string? responseBody = null)
        : base(
            BuildMessage(
                stage,
                statusCode,
                Snippet(responseBody)
            ),
            null,
            statusCode)
    {
        Stage = stage;
        ResponseSnippet = Snippet(responseBody);
    }

    // Reads the reply body for the exception. Kroger's error replies
    // contain no credentials; the request's token is never included.
    public static async Task<KrogerApiException> FromResponseAsync(
        string stage,
        HttpResponseMessage response)
    {
        string? body = null;

        try
        {
            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            // The status code alone still identifies the failure.
        }

        return new KrogerApiException(
            stage,
            response.StatusCode,
            body
        );
    }

    private static string BuildMessage(
        string stage,
        HttpStatusCode statusCode,
        string? snippet)
    {
        var message =
            $"Kroger {stage} request failed with {(int)statusCode} {statusCode}.";

        return string.IsNullOrEmpty(snippet)
            ? message
            : $"{message} Response: {snippet}";
    }

    private static string? Snippet(
        string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var collapsed =
            Regex.Replace(body, @"\s+", " ").Trim();

        return collapsed.Length <= MaxSnippetLength
            ? collapsed
            : collapsed[..MaxSnippetLength] + "…";
    }
}
