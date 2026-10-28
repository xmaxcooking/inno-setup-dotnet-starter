using App.Updates.Feed;
using App.Updates.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Updates.Tests;

[TestClass]
public class UpdateSettingsTests
{
    [TestMethod]
    public void GetChannel_ReturnsStable_WhenNoSettingsFileExists()
    {
        var fileSystem = new FakeFileSystem();
        var defaultUpdateChannel = new FakeDefaultUpdateChannel();
        var logger = NullLogger<UpdateSettings>.Instance;
        var settings = new UpdateSettings(fileSystem, defaultUpdateChannel, logger);

        var channel = settings.GetChannel();

        Assert.AreEqual(UpdateChannel.Stable, channel);
    }

    [TestMethod]
    public void GetChannel_ReturnsTheStoredChannel_WhenTheFileContainsValidJson()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile("""{"Channel":"Current"}""");
        var defaultUpdateChannel = new FakeDefaultUpdateChannel();
        var logger = NullLogger<UpdateSettings>.Instance;
        var settings = new UpdateSettings(fileSystem, defaultUpdateChannel, logger);

        var channel = settings.GetChannel();

        Assert.AreEqual(UpdateChannel.Current, channel);
    }

    [TestMethod]
    public void GetChannel_ReturnsStable_WhenTheStoredJsonIsCorrupt()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile("not valid json");
        var defaultUpdateChannel = new FakeDefaultUpdateChannel();
        var logger = NullLogger<UpdateSettings>.Instance;
        var settings = new UpdateSettings(fileSystem, defaultUpdateChannel, logger);

        var channel = settings.GetChannel();

        Assert.AreEqual(UpdateChannel.Stable, channel);
    }

    [TestMethod]
    public void SetChannel_PersistsTheChannel_SoItCanBeReadBack()
    {
        var fileSystem = new FakeFileSystem();
        var defaultUpdateChannel = new FakeDefaultUpdateChannel();
        var logger = NullLogger<UpdateSettings>.Instance;
        var settings = new UpdateSettings(fileSystem, defaultUpdateChannel, logger);

        settings.SetChannel(UpdateChannel.Current);
        var channel = settings.GetChannel();

        Assert.AreEqual(UpdateChannel.Current, channel);
        Assert.IsTrue(fileSystem.CreatedDirectories.Count > 0);
    }

    [TestMethod]
    public void SetChannel_DoesNotThrow_WhenTheFileSystemThrows()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.ThrowOnWriteAllText = true;
        var defaultUpdateChannel = new FakeDefaultUpdateChannel();
        var logger = NullLogger<UpdateSettings>.Instance;
        var settings = new UpdateSettings(fileSystem, defaultUpdateChannel, logger);

        settings.SetChannel(UpdateChannel.Current);
    }

    [TestMethod]
    public void SetChannel_ResetsTheUpdateCheckCooldown_SoTheNextLaunchChecksImmediately()
    {
        var fileSystem = new FakeFileSystem();
        var defaultUpdateChannel = new FakeDefaultUpdateChannel();
        var logger = NullLogger<UpdateSettings>.Instance;
        var settings = new UpdateSettings(fileSystem, defaultUpdateChannel, logger);

        settings.SetChannel(UpdateChannel.Current);

        Assert.AreEqual(1, fileSystem.DeletedFilePaths.Count);
    }

    [TestMethod]
    public void GetChannel_ReturnsTheDefaultChannel_WhenNoSettingsFileExists()
    {
        var fileSystem = new FakeFileSystem();
        var defaultUpdateChannel = new FakeDefaultUpdateChannel { Value = UpdateChannel.Current };
        var logger = NullLogger<UpdateSettings>.Instance;
        var settings = new UpdateSettings(fileSystem, defaultUpdateChannel, logger);

        var channel = settings.GetChannel();

        Assert.AreEqual(UpdateChannel.Current, channel);
    }

    [TestMethod]
    public void GetChannel_ReturnsTheDefaultChannel_WhenTheStoredJsonIsCorrupt()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile("not valid json");
        var defaultUpdateChannel = new FakeDefaultUpdateChannel { Value = UpdateChannel.Current };
        var logger = NullLogger<UpdateSettings>.Instance;
        var settings = new UpdateSettings(fileSystem, defaultUpdateChannel, logger);

        var channel = settings.GetChannel();

        Assert.AreEqual(UpdateChannel.Current, channel);
    }
}