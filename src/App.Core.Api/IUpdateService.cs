using App.Updates.Feed;

namespace App.Core.Api;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();

    Task DownloadAndLaunchUpdateAsync(UpdateInfo update, IProgress<double>? progress = null);
}