using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.AutoFilm;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmOpenListClientTests
{
    [Fact]
    public async Task GetObjectAsync_RefreshRequested_SendsRefreshFlag()
    {
        const string openListUrl = "http://openlist.test/";
        var previousUrl = Environment.GetEnvironmentVariable("AUTOFILM_OPENLIST_URL");
        var previousPublicUrl = Environment.GetEnvironmentVariable(
            "AUTOFILM_OPENLIST_PUBLIC_URL");
        var previousToken = Environment.GetEnvironmentVariable(
            "AUTOFILM_OPENLIST_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("AUTOFILM_OPENLIST_URL", openListUrl);
            Environment.SetEnvironmentVariable(
                "AUTOFILM_OPENLIST_PUBLIC_URL",
                openListUrl);
            Environment.SetEnvironmentVariable("AUTOFILM_OPENLIST_TOKEN", "test-token");

            var handler = new CaptureHandler();
            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory
                .Setup(instance => instance.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handler));
            var client = new AutoFilmOpenListClient(
                httpClientFactory.Object,
                new AutoFilmOptions());

            var result = await client.GetObjectAsync(
                "/115/movie/completed-download",
                true,
                TestContext.Current.CancellationToken);

            Assert.Null(result);
            Assert.NotNull(handler.RequestBody);
            using var body = JsonDocument.Parse(handler.RequestBody);
            Assert.Equal(
                "/115/movie/completed-download",
                body.RootElement.GetProperty("path").GetString());
            Assert.True(body.RootElement.GetProperty("refresh").GetBoolean());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTOFILM_OPENLIST_URL", previousUrl);
            Environment.SetEnvironmentVariable(
                "AUTOFILM_OPENLIST_PUBLIC_URL",
                previousPublicUrl);
            Environment.SetEnvironmentVariable(
                "AUTOFILM_OPENLIST_TOKEN",
                previousToken);
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"code":404,"message":"object not found","data":null}""",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
