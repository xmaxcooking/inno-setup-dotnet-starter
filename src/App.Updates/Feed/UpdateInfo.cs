namespace App.Updates.Feed;

public sealed class UpdateInfo
{
    public string Version { get; }
    public string DownloadUrl { get; }

    public UpdateInfo(string version, string downloadUrl)
    {
        Version = version;
        DownloadUrl = downloadUrl;
    }
}