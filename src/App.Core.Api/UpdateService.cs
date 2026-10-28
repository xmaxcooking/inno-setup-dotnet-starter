using App.Updates;
using App.Updates.Feed;

namespace App.Core.Api;

internal sealed class UpdateService : IUpdateService
{
    private readonly UpdateCheck _updateCheck;

    public UpdateService(UpdateCheck updateCheck)
    {
        _updateCheck = updateCheck;
    }

    public Task<UpdateCheckResult> CheckForUpdateAsync() => _updateCheck.CheckAsync();

    public Task DownloadAndLaunchUpdateAsync(UpdateInfo update, IProgress<double>? progress = null) =>
        _updateCheck.DownloadAndLaunchAsync(update, progress);
}