using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace App.Updates.Feed;

public sealed class GitHubReleaseUpdateFeed : IUpdateFeed
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;
    private readonly UpdateChannel _channel;
    private readonly ILogger _log;

    public GitHubReleaseUpdateFeed(
        HttpClient httpClient,
        string owner,
        string repo,
        UpdateChannel channel = UpdateChannel.Stable,
        ILogger<GitHubReleaseUpdateFeed>? logger = null)
    {
        _httpClient = httpClient;
        _owner = owner;
        _repo = repo;
        _channel = channel;
        _log = logger ?? NullLogger<GitHubReleaseUpdateFeed>.Instance;
    }

    public async Task<UpdateInfo?> TryGetLatestIfNewerAsync(string currentVersion, string clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = _channel == UpdateChannel.Stable
                ? $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest"
                : $"https://api.github.com/repos/{_owner}/{_repo}/releases?per_page=20";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd($"{_repo}-update-check");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("X-Client-Id", clientId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogDebug("Update feed ({Channel}) returned {StatusCode}; treating as no update.", _channel, response.StatusCode);
                return null;
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(), cancellationToken: cancellationToken);

            var release = selectRelease(document.RootElement, _channel);

            var update = release is { } r ? toUpdateInfo(r, currentVersion) : null;
            _log.LogDebug("Update check ({Channel}) for {CurrentVersion}: {Result}", _channel, currentVersion,
                update is null ? "up to date" : $"found {update.Version}");
            return update;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Update check ({Channel}) failed; treating as no update.", _channel);
            return null;
        }
    }

    // The releases list endpoint's order isn't reliably newest-first in practice (confirmed
    // against the real API: it can return older releases ahead of newer ones), so position 0
    // can't be trusted. Pick the entry with the highest build number among the fetched page
    // instead - BuildTag.TryParse already defines "newer" the same way IsNewer below does.
    private static JsonElement? selectRelease(JsonElement root, UpdateChannel channel)
    {
        if (channel == UpdateChannel.Stable)
        {
            return root;
        }

        JsonElement? latest = null;
        var latestRunNumber = -1L;
        foreach (var release in root.EnumerateArray())
        {
            var tagName = release.GetProperty("tag_name").GetString();
            if (tagName is null || !BuildTag.TryParse(tagName, out var runNumber))
            {
                continue;
            }

            if (latest is null || runNumber > latestRunNumber)
            {
                latest = release;
                latestRunNumber = runNumber;
            }
        }

        return latest;
    }

    private static UpdateInfo? toUpdateInfo(JsonElement release, string currentVersion)
    {
        var version = release.GetProperty("tag_name").GetString();
        if (version is null || version.Length == 0 || !BuildTag.IsNewer(currentVersion, version))
        {
            return null;
        }

        var asset = release.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("name").GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

        return asset.ValueKind != JsonValueKind.Object
            ? null
            : new UpdateInfo(version, asset.GetProperty("browser_download_url").GetString()!);
    }
}