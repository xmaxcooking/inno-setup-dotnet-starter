using App.Updates.Feed;
using System.Net;

namespace App.Updates.Tests;

[TestClass]
public class GitHubReleaseUpdateFeedTests
{
    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_RequestsTheReleasesList()
    {
        var handler = new RecordingHandler("""[{"tag_name":"v1.0.1","assets":[]}]""");
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        await feed.TryGetLatestIfNewerAsync("v1.0.0", "client-1");

        Assert.AreEqual("https://api.github.com/repos/owner/repo/releases?per_page=100", handler.RequestedUri);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_SendsTheClientIdAsAHeader()
    {
        var handler = new RecordingHandler("""[{"tag_name":"v1.0.1","assets":[]}]""");
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        await feed.TryGetLatestIfNewerAsync("v1.0.0", "client-42");

        Assert.AreEqual("client-42", handler.RequestedClientId);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_NotFound_ReturnsNone()
    {
        var handler = new RecordingHandler("", HttpStatusCode.NotFound);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        var result = await feed.TryGetLatestIfNewerAsync("v1.0.0", "client-1");

        Assert.IsNull(result.Update);
        Assert.IsNull(result.NewerMajorVersion);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Current_PicksTheHighestVersion_RegardlessOfListOrder()
    {
        // The real releases list endpoint isn't reliably newest-first, so this deliberately
        // puts the highest version (v1.0.12) in the middle rather than at index 0.
        var handler = new RecordingHandler("""
            [
                {"tag_name":"v1.0.5","assets":[]},
                {"tag_name":"v1.0.12","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v1.0.12.exe"}]},
                {"tag_name":"v1.0.9","assets":[]}
            ]
            """);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Current);

        var result = await feed.TryGetLatestIfNewerAsync("v1.0.1", "client-1");

        Assert.IsNotNull(result.Update);
        Assert.AreEqual("v1.0.12", result.Update.Version);
        Assert.AreEqual("https://example.com/v1.0.12.exe", result.Update.DownloadUrl);
        Assert.IsNull(result.NewerMajorVersion);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Current_PicksAnyNewerMajor_WithoutRestriction()
    {
        var handler = new RecordingHandler("""
            [
                {"tag_name":"v1.5.0","assets":[]},
                {"tag_name":"v3.0.0","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v3.0.0.exe"}]}
            ]
            """);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Current);

        var result = await feed.TryGetLatestIfNewerAsync("v1.0.0", "client-1");

        Assert.AreEqual("v3.0.0", result.Update?.Version);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Stable_HoldsBackANewMajor_AndReportsItAsANotice()
    {
        // Client is on v1.0.5; the newest major anywhere is v2, so Stable's ceiling is
        // max(1, 2-1) = 1 - v2.0.0 must not be auto-installed, only surfaced as a notice.
        var handler = new RecordingHandler("""
            [
                {"tag_name":"v1.0.9","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v1.0.9.exe"}]},
                {"tag_name":"v2.0.0","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v2.0.0.exe"}]}
            ]
            """);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        var result = await feed.TryGetLatestIfNewerAsync("v1.0.5", "client-1");

        Assert.AreEqual("v1.0.9", result.Update?.Version);
        Assert.AreEqual("v2.0.0", result.NewerMajorVersion);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Stable_ClimbsOneMajorAtATime_NeverSkippingAhead()
    {
        // Client is on v3.x; the newest major anywhere is v5, so Stable's ceiling is
        // max(3, 5-1) = 4 - it advances to v4, not straight to v5.
        var handler = new RecordingHandler("""
            [
                {"tag_name":"v3.9.0","assets":[]},
                {"tag_name":"v4.2.0","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v4.2.0.exe"}]},
                {"tag_name":"v5.0.0","assets":[]}
            ]
            """);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        var result = await feed.TryGetLatestIfNewerAsync("v3.0.0", "client-1");

        Assert.AreEqual("v4.2.0", result.Update?.Version);
        Assert.AreEqual("v5.0.0", result.NewerMajorVersion);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Stable_StillOffersPatches_WhenAlreadyOnTheCeilingMajor()
    {
        // Client is already on v4.0.0, the ceiling major (newest major anywhere is v5, so
        // ceiling = max(4, 5-1) = 4) - a patch within v4 must still be offered.
        var handler = new RecordingHandler("""
            [
                {"tag_name":"v4.0.1","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v4.0.1.exe"}]},
                {"tag_name":"v5.0.0","assets":[]}
            ]
            """);
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        var result = await feed.TryGetLatestIfNewerAsync("v4.0.0", "client-1");

        Assert.AreEqual("v4.0.1", result.Update?.Version);
        Assert.AreEqual("v5.0.0", result.NewerMajorVersion);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_Stable_TakesAMinorBump_WithinTheSameMajor()
    {
        // "Stable" only holds back a new major - Minor/Patch within the client's own major (or
        // the one it's climbing to) are never restricted.
        var handler = new RecordingHandler("""[{"tag_name":"v1.3.0","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v1.3.0.exe"}]}]""");
        var feed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);

        var result = await feed.TryGetLatestIfNewerAsync("v1.0.5", "client-1");

        Assert.AreEqual("v1.3.0", result.Update?.Version);
        Assert.IsNull(result.NewerMajorVersion);
    }

    [TestMethod]
    public async Task TryGetLatestIfNewerAsync_BothChannels_ConsiderAHotfixOnAnOlderLine_AsACandidate()
    {
        // v1.0.13 is a hotfix published after master had already moved on to v1.1.0 - it must
        // still be seen, even though it isn't the newest release overall.
        var handler = new RecordingHandler("""
            [
                {"tag_name":"v1.1.0","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v1.1.0.exe"}]},
                {"tag_name":"v1.0.13","assets":[{"name":"App.Bundle.exe","browser_download_url":"https://example.com/v1.0.13.exe"}]}
            ]
            """);

        var currentFeed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Current);
        var currentResult = await currentFeed.TryGetLatestIfNewerAsync("v1.0.12", "client-1");
        Assert.AreEqual("v1.1.0", currentResult.Update?.Version);

        var stableFeed = new GitHubReleaseUpdateFeed(new HttpClient(handler), "owner", "repo", UpdateChannel.Stable);
        var stableResult = await stableFeed.TryGetLatestIfNewerAsync("v1.0.12", "client-1");
        Assert.AreEqual("v1.1.0", stableResult.Update?.Version);
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