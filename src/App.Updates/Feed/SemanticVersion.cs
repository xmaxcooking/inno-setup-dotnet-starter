namespace App.Updates.Feed;

public static class SemanticVersion
{
    public static string Format(int major, int minor, int patch) => $"v{major}.{minor}.{patch}";

    // Build metadata (the part after '+', including the SDK's auto-appended git commit sha -
    // see Build.cs) is explicitly excluded from version precedence by the SemVer spec, so it's
    // stripped here rather than treated as a parse failure.
    public static Version? Parse(string version)
    {
        var withoutMetadata = stripBuildMetadata(version);
        var core = withoutMetadata.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? withoutMetadata.Substring(1)
            : withoutMetadata;
        return Version.TryParse(core, out var parsed) ? parsed : null;
    }

    public static bool IsNewer(string current, string latest)
    {
        var latestVersion = Parse(latest);
        var currentVersion = Parse(current);
        return latestVersion is not null && currentVersion is not null && latestVersion > currentVersion;
    }

    private static string stripBuildMetadata(string version)
    {
        var plus = version.IndexOf('+');
        return plus >= 0 ? version.Substring(0, plus) : version;
    }
}