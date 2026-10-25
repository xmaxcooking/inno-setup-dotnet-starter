using App.Updates.Feed;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Text.Json;

namespace App.Updates.Tests;

[TestClass]
public class UpdateCheckTests
{
    [TestMethod]
    public async Task CheckAsync_ReturnsNull_AndSkipsTheFeed_WhenCheckedRecently()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile(seedState(runningVersion(), now.AddMinutes(-10)));
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.CheckAsync();

        Assert.IsNull(result);
        Assert.IsFalse(feed.WasCalled);
        Assert.IsNull(fileSystem.WrittenContent);
    }

    [TestMethod]
    public async Task CheckAsync_ChecksAndReturnsNull_WhenTheCheckIntervalHasElapsed()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile(seedState(runningVersion(), now.AddHours(-2)));
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.CheckAsync();

        Assert.IsNull(result);
        Assert.IsTrue(feed.WasCalled);
        Assert.AreEqual("the-client-id", feed.ReceivedClientId);
        assertWrittenState(fileSystem, runningVersion(), now);
        Assert.IsTrue(fileSystem.CreatedDirectories.Count > 0);
    }

    [TestMethod]
    public async Task CheckAsync_Checks_WhenNoPriorCheckWasRecorded()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.CheckAsync();

        Assert.IsNull(result);
        Assert.IsTrue(feed.WasCalled);
    }

    [TestMethod]
    public async Task CheckAsync_Checks_WhenTheStoredStateIsUnparsable()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile("not-a-timestamp");
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.CheckAsync();

        Assert.IsNull(result);
        Assert.IsTrue(feed.WasCalled);
    }

    [TestMethod]
    public async Task CheckAsync_Checks_WhenTheRecordedVersionDiffersFromTheRunningOne()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile(seedState("build-99", now.AddMinutes(-10)));
        var feed = new FakeUpdateFeed(null);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.CheckAsync();

        Assert.IsNull(result);
        Assert.IsTrue(feed.WasCalled);
    }

    [TestMethod]
    public async Task CheckAsync_ReturnsNull_AndNeverThrows_WhenTheFeedFails()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        var failure = new InvalidOperationException("simulated feed failure");
        var feed = FakeUpdateFeed.ThatFails(failure);
        var check = createUpdateCheck(feed, fileSystem, now);

        var result = await check.CheckAsync();

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CheckAsync_ReturnsTheUpdate_WithoutDownloadingOrLaunchingAnything_WhenAnUpdateIsAvailable()
    {
        var now = DateTimeOffset.UtcNow;
        var fileSystem = new FakeFileSystem();
        var update = new UpdateInfo("build-99", "https://example.com/installer.exe");
        var feed = new FakeUpdateFeed(update);
        var processLauncher = new FakeProcessLauncher();
        var check = createUpdateCheck(feed, fileSystem, now, processLauncher: processLauncher);

        var result = await check.CheckAsync();

        Assert.AreSame(update, result);
        Assert.AreEqual(0, fileSystem.CreatedFilePaths.Count);
        Assert.AreEqual(0, processLauncher.StartedPaths.Count);
        assertWrittenState(fileSystem, runningVersion(), now);
    }

    [TestMethod]
    public async Task DownloadAndLaunchAsync_DownloadsAndLaunchesTheInstaller()
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

        await check.DownloadAndLaunchAsync(update);

        Assert.AreEqual("https://example.com/installer.exe", handler.RequestedUri);
        Assert.AreEqual(1, fileSystem.CreatedFilePaths.Count);
        Assert.AreEqual(1, processLauncher.StartedPaths.Count);
        Assert.AreEqual(fileSystem.CreatedFilePaths[0], processLauncher.StartedPaths[0]);
        Assert.IsTrue(processLauncher.StartedPaths[0].Contains("build-99"));
    }

    // Mirrors UpdateCheck.CheckAsync's own Assembly.GetExecutingAssembly() call - that resolves
    // to App.Updates.dll regardless of which assembly calls into it, so reading the attribute
    // from the same assembly here gives the exact value UpdateCheck itself will compare against,
    // whatever a local build's InformationalVersion (and any +<git-sha> suffix) happens to be.
    private static string runningVersion() =>
        typeof(UpdateCheck).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "build-0";

    private static string seedState(string version, DateTimeOffset checkedAt)
    {
        var state = new { Version = version, CheckedAt = checkedAt };
        return JsonSerializer.Serialize(state);
    }

    private static void assertWrittenState(FakeFileSystem fileSystem, string expectedVersion, DateTimeOffset expectedCheckedAt)
    {
        using var document = JsonDocument.Parse(fileSystem.WrittenContent!);
        Assert.AreEqual(expectedVersion, document.RootElement.GetProperty("Version").GetString());
        Assert.AreEqual(expectedCheckedAt, document.RootElement.GetProperty("CheckedAt").GetDateTimeOffset());
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