using App.Abstractions.FileSystem;
using App.Abstractions.Processes;
using App.Abstractions.Time;
using App.Updates.Feed;
using App.Updates.FileSystem;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace App.Updates;

public sealed class UpdateCheck
{
    private static readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan _overallTimeout = TimeSpan.FromSeconds(20);

    private readonly IUpdateFeed _feed;
    private readonly IClientIdentity _clientIdentity;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISystemDateTime _systemDateTime;
    private readonly IFileSystem _fileSystem;
    private readonly IProcessLauncher _processLauncher;
    private readonly ILogger<UpdateCheck> _logger;

    public UpdateCheck(
        IUpdateFeed feed, IClientIdentity clientIdentity, IHttpClientFactory httpClientFactory, ISystemDateTime systemDateTime,
        IFileSystem fileSystem, IProcessLauncher processLauncher, ILogger<UpdateCheck> logger)
    {
        _feed = feed;
        _clientIdentity = clientIdentity;
        _httpClientFactory = httpClientFactory;
        _systemDateTime = systemDateTime;
        _fileSystem = fileSystem;
        _processLauncher = processLauncher;
        _logger = logger;
    }

    public async Task<bool> RunIfDueAsync()
    {
        try
        {
            if (!isDue())
            {
                _logger.LogDebug("Update check skipped; not due yet.");
                return false;
            }

            using var cts = new CancellationTokenSource(_overallTimeout);

            var currentVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "build-0";

            var update = await _feed.TryGetLatestIfNewerAsync(currentVersion, _clientIdentity.GetOrCreate(), cts.Token);

            recordChecked();

            if (update is null)
            {
                return false;
            }

            await downloadAndLaunchAsync(update, cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed; continuing to start normally.");
            return false;
        }
    }

    private async Task downloadAndLaunchAsync(UpdateInfo update, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Update {Version} found; downloading and launching installer.", update.Version);

        var installerPath = Path.Combine(Path.GetTempPath(), $"inno-setup-dotnet-starter-{update.Version}.exe");

        var httpClient = _httpClientFactory.CreateClient(nameof(UpdateCheck));
        using (var source = await httpClient.GetStreamAsync(update.DownloadUrl))
        {
            using (var destination = _fileSystem.CreateFile(installerPath))
            {
                await source.CopyToAsync(destination, 81920, cancellationToken);
            }
        }

        _processLauncher.Start(installerPath);
    }

    private bool isDue()
    {
        try
        {
            if (!_fileSystem.FileExists(SpecialFiles.LastUpdateCheck))
            {
                return true;
            }

            return !DateTimeOffset.TryParse(_fileSystem.ReadAllText(SpecialFiles.LastUpdateCheck), out var lastChecked)
                || _systemDateTime.UtcNow - lastChecked >= _checkInterval;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read update-check state file; checking now instead.");
            return true;
        }
    }

    private void recordChecked()
    {
        try
        {
            _fileSystem.CreateDirectory(ApplicationDataDirectory.Path);
            _fileSystem.WriteAllText(SpecialFiles.LastUpdateCheck, _systemDateTime.UtcNow.ToString("O"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist update-check timestamp.");
        }
    }
}