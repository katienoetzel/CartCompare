using System.Net;
using CartCompare.Infrastructure.Providers.Kroger;

namespace CartCompare.IntegrationTests.Providers;

public class KrogerApiExceptionTests
{
    [Fact]
    public async Task FromResponseAsync_KeepsTheStartOfKrogersReply()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                "<HTML><HEAD>\n<TITLE>Access Denied</TITLE>\n</HEAD><BODY>Reference #18.abc</BODY></HTML>")
        };

        var exception = await KrogerApiException.FromResponseAsync("locations", response);

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(
            "Kroger locations request failed with 403 Forbidden. Response: " +
            "<HTML><HEAD> <TITLE>Access Denied</TITLE> </HEAD><BODY>Reference #18.abc</BODY></HTML>",
            exception.Message);
    }

    [Fact]
    public async Task FromResponseAsync_ShortensLongReplies()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent(new string('x', 1000))
        };

        var exception = await KrogerApiException.FromResponseAsync("price", response);

        Assert.Equal(301, exception.ResponseSnippet!.Length);
        Assert.EndsWith("…", exception.ResponseSnippet);
    }

    [Fact]
    public void WithoutABody_TheMessageNamesOnlyTheStatus()
    {
        var exception = new KrogerApiException("token", HttpStatusCode.Unauthorized);

        Assert.Equal("Kroger token request failed with 401 Unauthorized.", exception.Message);
        Assert.Null(exception.ResponseSnippet);
    }
}
