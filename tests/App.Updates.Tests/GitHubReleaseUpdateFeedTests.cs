using App.Updates.Feed;
using System.Net;

namespace App.Updates.Tests;

[TestClass]
public class GitHubReleaseUpdateFeedTests
{
    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Stable_RequestsReleasesLatest()
    {
        var handler = new RecordingHandler("""{"tag_name":"build-1","assets":[]}""");
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        await feed.TryGetLatestIfNewerAsync("build-0", "client-1");

        Assert.AreEqual("https://api.github.com/repos/owner/repo/releases/latest", handler.RequestedUri);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Beta_RequestsReleasesList()
    {
        var handler = new RecordingHandler("""[{"tag_name":"build-1","assets":[]}]""");
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Beta);

        await feed.TryGetLatestIfNewerAsync("build-0", "client-1");

        Assert.AreEqual("https://api.github.com/repos/owner/repo/releases?per_page=20", handler.RequestedUri);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Beta_PicksTheHighestBuildNumber_RegardlessOfListOrder()
    {
        // The real releases list endpoint isn't reliably newest-first, so this deliberately
        // puts the highest build number (12) in the middle rather than at index 0.
        var handler = new RecordingHandler("""
            [
                {"tag_name":"build-5","assets":[]},
                {"tag_name":"build-12","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/build-12.exe"}]},
                {"tag_name":"build-9","assets":[]}
            ]
            """);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Beta);

        var update = await feed.TryGetLatestIfNewerAsync("build-1", "client-1");

        Assert.IsNotNull(update);
        Assert.AreEqual("build-12", update.Version);
        Assert.AreEqual("https://example.com/build-12.exe", update.DownloadUrl);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_SendsTheClientIdAsAHeader()
    {
        var handler = new RecordingHandler("""{"tag_name":"build-1","assets":[]}""");
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        await feed.TryGetLatestIfNewerAsync("build-0", "client-42");

        Assert.AreEqual("client-42", handler.RequestedClientId);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Stable_ParsesTheSingleReleaseObject()
    {
        var handler = new RecordingHandler("""
            {"tag_name":"build-5","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/App.Bundle.exe"}]}
            """);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        var update = await feed.TryGetLatestIfNewerAsync("build-1", "client-1");

        Assert.IsNotNull(update);
        Assert.AreEqual("build-5", update.Version);
        Assert.AreEqual("https://example.com/App.Bundle.exe", update.DownloadUrl);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_NotFound_ReturnsNull()
    {
        var handler = new RecordingHandler("", HttpStatusCode.NotFound);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        var update = await feed.TryGetLatestIfNewerAsync("build-0", "client-1");

        Assert.IsNull(update);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        public string? RequestedUri { get; private set; }
        public string? RequestedClientId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri?.ToString();
            RequestedClientId = request.Headers.TryGetValues("X-Client-Id", out var values) ? values.First() : null;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            });
        }
    }
}