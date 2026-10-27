using App.Updates.Feed;

namespace App.Updates.Tests;

[TestClass]
public class SemanticVersionTests
{
    [TestMethod]
    [DataRow("v1.0.1", "v1.0.2", true)]
    [DataRow("v1.0.2", "v1.0.1", false)]
    [DataRow("v1.0.5", "v1.0.5", false)]
    [DataRow("v1.0.9", "v1.0.10", true)]
    public void IsNewer_ComparesVersionsNumerically(string current, string latest, bool expected) =>
        Assert.AreEqual(expected, SemanticVersion.IsNewer(current, latest));

    [TestMethod]
    [DataRow("not-a-version")]
    [DataRow("v")]
    [DataRow("")]
    public void IsNewer_NeverThrowsOrSignalsAnUpdate_ForAnUnrecognizedVersion(string malformed) =>
        Assert.IsFalse(SemanticVersion.IsNewer(malformed, "v1.0.99"));

    [TestMethod]
    public void Parse_ParsesTheDottedVersion()
    {
        var version = SemanticVersion.Parse("v1.0.42");

        Assert.AreEqual(new Version(1, 0, 42), version);
    }

    [TestMethod]
    public void Parse_TreatsAPlusSuffixAsBuildMetadata_LikeTheSdkAppendedGitSha()
    {
        var version = SemanticVersion.Parse("v1.0.42+91f073f04eb1d7cd032fcddb583254cfbc59c23d");

        Assert.AreEqual(new Version(1, 0, 42), version);
    }

    [TestMethod]
    [DataRow("not-a-version")]
    [DataRow("v")]
    [DataRow("")]
    public void Parse_ReturnsNull_ForAnUnrecognizedVersion(string malformed) =>
        Assert.IsNull(SemanticVersion.Parse(malformed));

    [TestMethod]
    public void Format_RoundTripsThroughParse()
    {
        var tag = SemanticVersion.Format(2, 3, 7);

        Assert.AreEqual(new Version(2, 3, 7), SemanticVersion.Parse(tag));
    }
}