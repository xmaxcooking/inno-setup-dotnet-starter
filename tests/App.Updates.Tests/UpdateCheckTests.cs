using App.Updates.Feed;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Updates.Tests;

[TestClass]
public class UpdateCheckTests
{
    [TestMethod]
    public async Task RunIfDueAsync_ReturnsFalse_AndSkipsTheFeed_WhenCheckedRecently()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile(now.AddMinutes(-10).ToString("O"));
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.RunIfDueAsync();

        Assert.IsFalse(result);
        Assert.IsFalse(feed.WasCalled);
        Assert.IsNull(fileSystem.WrittenContent);
    }

    [TestMethod]
    public async Task RunIfDueAsync_ChecksAndReturnsFalse_WhenTheCheckIntervalHasElapsed()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile(now.AddHours(-2).ToString("O"));
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.RunIfDueAsync();

        Assert.IsFalse(result);
        Assert.IsTrue(feed.WasCalled);
        Assert.AreEqual("the-client-id", feed.ReceivedClientId);
        Assert.AreEqual(now.ToString("O"), fileSystem.WrittenContent);
        Assert.IsTrue(fileSystem.CreatedDirectories.Count > 0);
    }

    [TestMethod]
    public async Task RunIfDueAsync_Checks_WhenNoPriorCheckWasRecorded()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.RunIfDueAsync();

        Assert.IsFalse(result);
        Assert.IsTrue(feed.WasCalled);
    }

    [TestMethod]
    public async Task RunIfDueAsync_Checks_WhenTheStoredTimestampIsUnparsable()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile("not-a-timestamp");
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.RunIfDueAsync();

        Assert.IsFalse(result);
        Assert.IsTrue(feed.WasCalled);
    }

    [TestMethod]
    public async Task RunIfDueAsync_ReturnsFalse_AndNeverThrows_WhenTheFeedFails()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        var failure = new InvalidOperationException("simulated feed failure");
        var feed = FakeUpdateFeed.ThatFails(failure);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.RunIfDueAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task RunIfDueAsync_DownloadsAndLaunchesTheInstaller_WhenAnUpdateIsAvailable()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        var update = new UpdateInfo("build-99", "https://example.com/installer.exe");
        var feed = new FakeUpdateFeed(update);
        var handler = new StubHttpMessageHandler("installer-bytes");
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new FakeHttpClientFactory(httpClient);
        var processLauncher = new FakeProcessLauncher();
        var check = createUpdateCheck(feed, fileSystem, now, httpClientFactory, processLauncher);

        var result = await check.RunIfDueAsync();

        Assert.IsTrue(result);
        Assert.AreEqual("https://example.com/installer.exe", handler.RequestedUri);
        Assert.AreEqual(1, fileSystem.CreatedFilePaths.Count);
        Assert.AreEqual(1, processLauncher.StartedPaths.Count);
        Assert.AreEqual(fileSystem.CreatedFilePaths[0], processLauncher.StartedPaths[0]);
        Assert.IsTrue(processLauncher.StartedPaths[0].Contains("build-99"));
        Assert.AreEqual(now.ToString("O"), fileSystem.WrittenContent);
    }

    private static UpdateCheck createUpdateCheck(
        FakeUpdateFeed feed, FakeFileSystem fileSystem, DateTimeOffset now,
        IHttpClientFactory? httpClientFactory = null, FakeProcessLauncher? processLauncher = null)
    {
        var clientIdentity = new FakeClientIdentity("the-client-id");
        var resolvedHttpClientFactory = httpClientFactory ?? new ThrowingHttpClientFactory();
        var resolvedProcessLauncher = processLauncher ?? new FakeProcessLauncher();
        var systemDateTime = new FakeSystemDateTime(now);
        var logger = NullLogger<UpdateCheck>.Instance;
        return new UpdateCheck(
            feed, clientIdentity, resolvedHttpClientFactory, systemDateTime, fileSystem, resolvedProcessLauncher, logger);
    }
}