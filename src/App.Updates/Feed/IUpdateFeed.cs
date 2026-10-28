namespace App.Updates.Feed;

public interface IUpdateFeed
{
    Task<UpdateInfo?> TryGetLatestIfNewerAsync(string currentVersion, string clientId, CancellationToken cancellationToken = default);
}