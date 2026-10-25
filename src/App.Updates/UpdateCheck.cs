using App.Abstractions.FileSystem;
using App.Abstractions.Processes;
using App.Abstractions.Time;
using App.Updates.Feed;
using App.Updates.FileSystem;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

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

    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var currentVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "v0.0.0";

            if (!isDue(currentVersion))
            {
                _logger.LogDebug("Update check skipped; not due yet.");
                return null;
            }

            using var cts = new CancellationTokenSource(_overallTimeout);

            var update = await _feed.TryGetLatestIfNewerAsync(currentVersion, _clientIdentity.GetOrCreate(), cts.Token);

            recordChecked(currentVersion);

            return update;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed; continuing to start normally.");
            return null;
        }
    }

    public async Task DownloadAndLaunchAsync(UpdateInfo update)
    {
        _logger.LogInformation("Update {Version} confirmed by the user; downloading and launching installer.", update.Version);

        using var cts = new CancellationTokenSource(_overallTimeout);

        var installerPath = Path.Combine(Path.GetTempPath(), $"inno-setup-dotnet-starter-{update.Version}.exe");

        var httpClient = _httpClientFactory.CreateClient(nameof(UpdateCheck));
        using (var source = await httpClient.GetStreamAsync(update.DownloadUrl))
        {
            using (var destination = _fileSystem.CreateFile(installerPath))
            {
                await source.CopyToAsync(destination, 81920, cts.Token);
            }
        }

        _processLauncher.Start(installerPath);
    }

    // Due regardless of the cooldown below whenever the running version differs from the one
    // recorded by the last check - otherwise a build published and installed within the same
    // hour as an earlier build's "no update" check would silently inherit that cooldown and
    // skip checking on its very first launch, right when a check is most wanted.
    private bool isDue(string currentVersion)
    {
        try
        {
            if (!_fileSystem.FileExists(SpecialFiles.LastUpdateCheck))
            {
                return true;
            }

            var state = JsonSerializer.Deserialize<CheckState>(_fileSystem.ReadAllText(SpecialFiles.LastUpdateCheck));
            return state is null
                || state.Version != currentVersion
                || _systemDateTime.UtcNow - state.CheckedAt >= _checkInterval;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read update-check state file; checking now instead.");
            return true;
        }
    }

    private void recordChecked(string currentVersion)
    {
        try
        {
            _fileSystem.CreateDirectory(ApplicationDataDirectory.Path);
            var state = new CheckState(currentVersion, _systemDateTime.UtcNow);
            _fileSystem.WriteAllText(SpecialFiles.LastUpdateCheck, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist update-check timestamp.");
        }
    }

    private sealed class CheckState
    {
        public string Version { get; }
        public DateTimeOffset CheckedAt { get; }

        public CheckState(string version, DateTimeOffset checkedAt)
        {
            Version = version;
            CheckedAt = checkedAt;
        }
    }
}