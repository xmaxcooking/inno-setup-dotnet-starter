namespace App.Updates.Feed;

public interface IUpdateFeed
{
    Task<UpdateCheckResult> TryGetLatestIfNewerAsync(string currentVersion, string clientId, CancellationToken cancellationToken = default);
}