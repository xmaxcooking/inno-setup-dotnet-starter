using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace App.Updates.Feed;

internal sealed class GitHubReleaseUpdateFeed : IUpdateFeed
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

    public async Task<UpdateCheckResult> TryGetLatestIfNewerAsync(string currentVersion, string clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var current = SemanticVersion.Parse(currentVersion);
            if (current is null)
            {
                _log.LogDebug("Could not parse current version {CurrentVersion}; treating as no update.", currentVersion);
                return UpdateCheckResult.None;
            }

            var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases?per_page=100";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd($"{_repo}-update-check");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("X-Client-Id", clientId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogDebug("Update feed ({Channel}) returned {StatusCode}; treating as no update.", _channel, response.StatusCode);
                return UpdateCheckResult.None;
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(), cancellationToken: cancellationToken);

            var result = selectResult(document.RootElement, _channel, current);

            _log.LogDebug("Update check ({Channel}) for {CurrentVersion}: {Result}", _channel, currentVersion,
                result.Update is null ? "up to date" : $"found {result.Update.Version}");
            return result;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Update check ({Channel}) failed; treating as no update.", _channel);
            return UpdateCheckResult.None;
        }
    }

    // The releases list endpoint's order isn't reliably newest-first in practice (confirmed
    // against the real API: it can return older releases ahead of newer ones), so position 0
    // can't be trusted - every candidate is compared by its parsed version instead.
    //
    // Current always takes the single highest version found anywhere (including hotfixes on an
    // older line - they're just ordinary candidates, nothing excludes them). Stable never reaches
    // the single newest major that exists: its ceiling is max(client's own major, highest major
    // anywhere - 1) - so it climbs toward "one behind the bleeding edge" and, once there (or
    // already past it on its own major), keeps taking Minor/Patch updates within that major, but
    // never the newest major itself. Anything beyond that ceiling is reported as NewerMajorVersion
    // instead - a notice, never an install prompt.
    private static UpdateCheckResult selectResult(JsonElement root, UpdateChannel channel, Version current)
    {
        var candidates = new List<(Version Version, JsonElement Release)>();
        foreach (var release in root.EnumerateArray())
        {
            var tagName = release.GetProperty("tag_name").GetString();
            var version = tagName is null ? null : SemanticVersion.Parse(tagName);
            if (version is not null)
            {
                candidates.Add((version, release));
            }
        }

        if (candidates.Count == 0)
        {
            return UpdateCheckResult.None;
        }

        if (channel == UpdateChannel.Current)
        {
            var highest = highestOf(candidates);
            var currentUpdate = highest.Version > current ? toUpdateInfo(highest.Release) : null;
            return new UpdateCheckResult(currentUpdate, null);
        }

        var globalMaxMajor = candidates.Max(c => c.Version.Major);
        var ceilingMajor = Math.Max(current.Major, globalMaxMajor - 1);

        var withinCeiling = candidates.Where(c => c.Version.Major == ceilingMajor && c.Version > current).ToList();
        var update = withinCeiling.Count > 0 ? toUpdateInfo(highestOf(withinCeiling).Release) : null;

        var beyondCeiling = candidates.Where(c => c.Version.Major > ceilingMajor).ToList();
        string? newerMajor = null;
        if (beyondCeiling.Count > 0)
        {
            var highestBeyond = highestOf(beyondCeiling).Version;
            newerMajor = SemanticVersion.Format(highestBeyond.Major, highestBeyond.Minor, highestBeyond.Build);
        }

        return new UpdateCheckResult(update, newerMajor);
    }

    private static (Version Version, JsonElement Release) highestOf(List<(Version Version, JsonElement Release)> candidates)
    {
        var best = candidates[0];
        foreach (var candidate in candidates)
        {
            if (candidate.Version > best.Version)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static UpdateInfo? toUpdateInfo(JsonElement release)
    {
        var version = release.GetProperty("tag_name").GetString();
        if (version is null || version.Length == 0)
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