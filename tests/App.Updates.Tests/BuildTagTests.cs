using App.Updates.Feed;

namespace App.Updates.Tests;

[TestClass]
public class BuildTagTests
{
    [TestMethod]
    [DataRow("build-1", "build-2", true)]
    [DataRow("build-2", "build-1", false)]
    [DataRow("build-5", "build-5", false)]
    public void IsNewer_ComparesRunNumbers(string current, string latest, bool expected) =>
        Assert.AreEqual(expected, BuildTag.IsNewer(current, latest));

    [TestMethod]
    [DataRow("not-a-build-tag")]
    [DataRow("build-")]
    [DataRow("")]
    public void IsNewer_NeverThrowsOrSignalsAnUpdate_ForAnUnrecognizedTag(string malformed) =>
        Assert.IsFalse(BuildTag.IsNewer(malformed, "build-99"));

    [TestMethod]
    public void TryParse_ParsesTheTrailingInteger()
    {
        var parsed = BuildTag.TryParse("build-42", out var runNumber);

        Assert.IsTrue(parsed);
        Assert.AreEqual(42L, runNumber);
    }

    [TestMethod]
    public void TryParse_TreatsAPlusSuffixAsBuildMetadata_LikeTheSdkAppendedGitSha()
    {
        var parsed = BuildTag.TryParse("build-42+91f073f04eb1d7cd032fcddb583254cfbc59c23d", out var runNumber);

        Assert.IsTrue(parsed);
        Assert.AreEqual(42L, runNumber);
    }

    [TestMethod]
    public void Format_RoundTripsThroughTryParse()
    {
        var tag = BuildTag.Format(7);

        Assert.IsTrue(BuildTag.TryParse(tag, out var runNumber));
        Assert.AreEqual(7L, runNumber);
    }
}