using App.Updates.Feed;

namespace App.Core.Api;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync();

    Task DownloadAndLaunchUpdateAsync(UpdateInfo update, IProgress<double>? progress = null);
}