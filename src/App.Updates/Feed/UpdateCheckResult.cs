namespace App.Updates.Feed;

public sealed class UpdateCheckResult
{
    public UpdateInfo? Update { get; }

    // Set only for the Stable channel, when a newer major version exists that Stable won't
    // auto-install - a passive "you could switch channels" notice, never an install prompt.
    public string? NewerMajorVersion { get; }

    public UpdateCheckResult(UpdateInfo? update, string? newerMajorVersion)
    {
        Update = update;
        NewerMajorVersion = newerMajorVersion;
    }

    public static UpdateCheckResult None { get; } = new(null, null);
}