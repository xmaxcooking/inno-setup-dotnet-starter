using Microsoft.Extensions.Logging.Abstractions;

namespace App.Updates.Tests;

[TestClass]
public class ClientIdentityTests
{
    [TestMethod]
    public void GetOrCreate_ReturnsTheStoredId_WhenItIsAValidGuid()
    {
        var storedId = Guid.NewGuid().ToString();
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile(storedId);
        var logger = NullLogger<ClientIdentity>.Instance;
        var identity = new ClientIdentity(fileSystem, logger);

        var result = identity.GetOrCreate();

        Assert.AreEqual(storedId, result);
        Assert.IsNull(fileSystem.WrittenContent);
    }

    [TestMethod]
    public void GetOrCreate_GeneratesAndPersistsANewId_WhenNoneIsStored()
    {
        var fileSystem = new FakeFileSystem();
        var logger = NullLogger<ClientIdentity>.Instance;
        var identity = new ClientIdentity(fileSystem, logger);

        var result = identity.GetOrCreate();

        Assert.IsTrue(Guid.TryParse(result, out _));
        Assert.AreEqual(result, fileSystem.WrittenContent);
        Assert.IsTrue(fileSystem.CreatedDirectories.Count > 0);
    }

    [TestMethod]
    public void GetOrCreate_GeneratesAndPersistsANewId_WhenTheStoredContentIsNotAValidGuid()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile("not-a-guid");
        var logger = NullLogger<ClientIdentity>.Instance;
        var identity = new ClientIdentity(fileSystem, logger);

        var result = identity.GetOrCreate();

        Assert.IsTrue(Guid.TryParse(result, out _));
        Assert.AreEqual(result, fileSystem.WrittenContent);
    }

    [TestMethod]
    public void GetOrCreate_ReturnsAOneRunOnlyId_WhenTheFileSystemThrows()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.SeedExistingFile(Guid.NewGuid().ToString());
        fileSystem.ThrowOnReadAllText = true;
        var logger = NullLogger<ClientIdentity>.Instance;
        var identity = new ClientIdentity(fileSystem, logger);

        var result = identity.GetOrCreate();

        Assert.IsTrue(Guid.TryParse(result, out _));
        Assert.IsNull(fileSystem.WrittenContent);
    }
}