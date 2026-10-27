using App.Updates.Feed;
using Fallout.Common;
using Fallout.Common.CI.GitHubActions;
using Fallout.Common.Git;
using Fallout.Common.IO;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Tools.GitHub;
using Fallout.Common.Utilities.Collections;
using Fallout.Solutions;
using Octokit;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static Fallout.Common.EnvironmentInfo;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// [GitHubActions] generates .github/workflows/release.yml from the attribute below; never hand-edit the .yml.
//
// master pushes and hotfix/** pushes both run this workflow, but only hotfix pushes (and the
// weekday/manual train below) actually publish a release - a plain master push only builds and
// tests, so pushing to master repeatedly during a day doesn't spam users with update prompts.
// See shouldPublishRelease.
[GitHubActions(
    "release",
    GitHubActionsImage.WindowsLatest,
    OnPushBranches = new[] { "master", "hotfix/**" },
    OnCronSchedule = "0 3 * * 1-5",
    InvokedTargets = new[] { nameof(publishRelease) },
    EnableGitHubToken = true,
    WritePermissions = new[] { GitHubActionsPermissions.Contents },
    PublishArtifacts = false,
    ConcurrencyGroup = "release-${{ github.ref }}",
    ConcurrencyCancelInProgress = false,
    FetchDepth = 0)]
// A separate workflow file for the manual-dispatch trigger - Fallout doesn't allow mixing the
// On*-style detailed triggers above (push/cron) with the shorthand On trigger list in one
// [GitHubActions] attribute, so this is a second, minimal workflow that invokes the same target.
[GitHubActions(
    "release-manual",
    GitHubActionsImage.WindowsLatest,
    On = new[] { GitHubActionsTrigger.WorkflowDispatch },
    InvokedTargets = new[] { nameof(publishRelease) },
    EnableGitHubToken = true,
    WritePermissions = new[] { GitHubActionsPermissions.Contents },
    PublishArtifacts = false,
    ConcurrencyGroup = "release-${{ github.ref }}",
    ConcurrencyCancelInProgress = false,
    FetchDepth = 0)]
internal class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.test);

    [Parameter("Whether this build is a prerelease - Default is true", Name = "IsPrereleaseBuild")]
    private readonly bool _isPrereleaseBuild = true;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)", Name = "Configuration")]
    private readonly Configuration _configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(Name = "Solution")] private readonly Solution _solution;
    [GitRepository] private readonly GitRepository _gitRepository;

    [Parameter("GitHub token used to create the release - set via GITHUB_TOKEN, provided automatically in Actions", Name = "GitHubToken")]
    [Secret]
    private readonly string _gitHubToken = GitHubActions.Instance?.Token;

    private AbsolutePath artifactsDirectory => RootDirectory / "artifacts";
    private AbsolutePath publishDirectory => RootDirectory / "artifacts" / "publish" / "App.Client.Windows";
    private AbsolutePath toolPublishDirectory => RootDirectory / "artifacts" / "publish" / "App.Installer.Tool";
    private AbsolutePath setupScript => RootDirectory / "installer" / "Setup.iss";

    // None < Patch < Minor < Major - deliberately ordered so the strongest bump found among a set
    // of commits can be picked with a plain `>` comparison.
    private enum VersionBump { None, Patch, Minor, Major }

    private (string Tag, VersionBump Bump)? _nextRelease;

    private (string Tag, VersionBump Bump) nextRelease
    {
        get
        {
            _nextRelease ??= computeNextRelease();
            return _nextRelease.Value;
        }
    }

    private string releaseTag => nextRelease.Tag;

    // A release is only worth publishing when there's something release-worthy since the last
    // one (nextRelease.Bump != None - a day/branch with only chore/docs commits, or none at all,
    // produces no release at all, same as real semantic-release) and, for a plain push, only when
    // it's a hotfix branch - a plain push to master always waits for the next train or a manual
    // dispatch instead, so pushing to master repeatedly doesn't spam users with update prompts.
    private bool shouldPublishRelease
    {
        get
        {
            if (nextRelease.Bump == VersionBump.None)
            {
                return false;
            }

            if (GitHubActions.Instance is not { } gitHubActions)
            {
                return true;
            }

            return gitHubActions.EventName != "push" || gitHubActions.RefName != "master";
        }
    }

    private string packageVersion => GitHubActions.Instance is { } gitHubActions
        ? $"1.0.{gitHubActions.RunNumber}.0"
        : $"1.0.{(DateTime.UtcNow.Date - new DateTime(2000, 1, 1)).Days}.{(int)(DateTime.UtcNow.TimeOfDay.TotalSeconds / 2)}";

    private string gitHubOwner => _gitRepository.GetGitHubOwner();
    private string gitHubRepoName => _gitRepository.GetGitHubName();

    private IReadOnlyList<Release> _existingReleases;

    private IReadOnlyList<Release> existingReleases()
    {
        if (_existingReleases is not null)
        {
            return _existingReleases;
        }

        if (_gitHubToken is { } token)
        {
            GitHubTasks.GitHubClient.Credentials = new Credentials(token);
        }

        _existingReleases = GitHubTasks.GitHubClient.Repository.Release
            .GetAll(gitHubOwner, gitHubRepoName, new ApiOptions { PageSize = 100 })
            .GetAwaiter()
            .GetResult();

        return _existingReleases;
    }

    // The base to bump from is the highest-versioned existing release that's actually an ancestor
    // of HEAD - not just the globally highest release - so a hotfix branch cut from an old v1.0.x
    // tag bumps from v1.0.x, even while master has already moved on to a newer v1.1.0/v2.0.0.
    private (int Major, int Minor, int Patch, string Sha) findLastRelease()
    {
        Version highestVersion = null;
        string highestSha = null;

        foreach (var release in existingReleases())
        {
            if (SemanticVersion.Parse(release.TagName) is not { } version
                || release.TargetCommitish is not { } sha
                || (highestVersion is not null && version <= highestVersion)
                || !isAncestorOfHead(sha))
            {
                continue;
            }

            highestVersion = version;
            highestSha = sha;
        }

        return highestVersion is null
            ? (0, 0, 0, null)
            : (highestVersion.Major, highestVersion.Minor, highestVersion.Build, highestSha);
    }

    private static bool isAncestorOfHead(string sha)
    {
        var startInfo = new ProcessStartInfo("git", $"merge-base --is-ancestor {sha} HEAD")
        {
            UseShellExecute = false,
            WorkingDirectory = RootDirectory
        };

        using var process = Process.Start(startInfo);
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    // Conventional Commits (https://www.conventionalcommits.org/), the same convention
    // semantic-release/GitVersion read: "feat:" -> Minor, "fix:" -> Patch, a "!" after the
    // type/scope or a "BREAKING CHANGE:" footer -> Major. Anything else (chore/docs/refactor/an
    // unconventional message) contributes no bump at all.
    private static VersionBump classifyCommitMessage(string message)
    {
        var subjectLine = message.Split('\n')[0];

        // A footer is its own paragraph (separated from the body by a blank line), per the spec -
        // not just the phrase appearing anywhere, which would false-positive on a commit message
        // that happens to mention "BREAKING CHANGE:" in passing (as this very check's own commit
        // message once did, describing this exact rule).
        var hasBreakingChangeFooter = message
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Any(paragraph => paragraph.StartsWith("BREAKING CHANGE:", StringComparison.Ordinal)
                || paragraph.StartsWith("BREAKING-CHANGE:", StringComparison.Ordinal));

        if (hasBreakingChangeFooter || Regex.IsMatch(subjectLine, @"^\w+(\([^)]*\))?!:"))
        {
            return VersionBump.Major;
        }

        if (Regex.IsMatch(subjectLine, @"^feat(\([^)]*\))?:", RegexOptions.IgnoreCase))
        {
            return VersionBump.Minor;
        }

        if (Regex.IsMatch(subjectLine, @"^fix(\([^)]*\))?:", RegexOptions.IgnoreCase))
        {
            return VersionBump.Patch;
        }

        return VersionBump.None;
    }

    private static IReadOnlyList<string> commitMessagesSince(string sha)
    {
        var range = sha is null ? "HEAD" : $"{sha}..HEAD";

        var startInfo = new ProcessStartInfo("git", $"log {range} --pretty=format:%B%x00")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            WorkingDirectory = RootDirectory
        };

        using var process = Process.Start(startInfo);
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    // Every commit must follow Conventional Commits (feat:/fix:/chore:/etc.) - not just the
    // feat:/fix:/breaking-change ones that actually affect the version, since an unconventional
    // message among those would silently contribute no bump at all (see classifyCommitMessage)
    // rather than being caught as the mistake it almost certainly is.
    private static readonly string[] _conventionalCommitTypes =
        ["feat", "fix", "build", "chore", "ci", "docs", "style", "refactor", "perf", "test", "revert"];

    private static IReadOnlyList<string> commitMessagesToVerify()
    {
        if (GitHubActions.Instance is not { } gitHubActions)
        {
            return commitMessagesSince("HEAD~1");
        }

        if (gitHubActions.EventName != "push")
        {
            return [];
        }

        var beforeSha = gitHubActions.GitHubEvent["before"]?.GetValue<string>();
        var isFirstPushToThisRef = beforeSha is null || beforeSha == new string('0', 40);

        return commitMessagesSince(isFirstPushToThisRef ? "HEAD~1" : beforeSha);
    }

    private Target verifyCommitMessages => _ => _
        .Executes(() =>
        {
            var typePattern = string.Join("|", _conventionalCommitTypes);
            var subjectPattern = new Regex($@"^({typePattern})(\([^)]*\))?!?: .+");

            foreach (var message in commitMessagesToVerify())
            {
                var subjectLine = message.Split('\n')[0];
                if (!subjectPattern.IsMatch(subjectLine))
                {
                    throw new InvalidOperationException(
                        $"Commit message doesn't follow Conventional Commits (https://www.conventionalcommits.org/) - "
                        + $"expected \"<{typePattern}>(optional-scope)!: description\", got: \"{subjectLine}\"");
                }
            }
        });

    private (string Tag, VersionBump Bump) computeNextRelease()
    {
        if (GitHubActions.Instance is null)
        {
            return (SemanticVersion.Format(1, 0, 0), VersionBump.Patch);
        }

        var (baseMajor, baseMinor, basePatch, baseSha) = findLastRelease();

        var bump = VersionBump.None;
        foreach (var message in commitMessagesSince(baseSha))
        {
            var commitBump = classifyCommitMessage(message);
            if (commitBump > bump)
            {
                bump = commitBump;
            }
        }

        var tag = bump switch
        {
            VersionBump.Major => SemanticVersion.Format(baseMajor + 1, 0, 0),
            VersionBump.Minor => SemanticVersion.Format(baseMajor, baseMinor + 1, 0),
            VersionBump.Patch => SemanticVersion.Format(baseMajor, baseMinor, basePatch + 1),
            VersionBump.None => SemanticVersion.Format(baseMajor, baseMinor, basePatch),
            _ => throw new InvalidOperationException($"Unrecognized {nameof(VersionBump)}: {bump}")
        };

        return (tag, bump);
    }

    private Target clean => _ => _
        .Before(restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("src/**/bin", "src/**/obj", "tests/**/bin", "tests/**/obj", "installer/**/bin", "installer/**/obj").ForEach(x => x.DeleteDirectory());
            artifactsDirectory.CreateOrCleanDirectory();
        });

    private Target restore => _ => _
        .Executes(() => DotNetRestore(s => s
            .SetProjectFile(_solution)));

    private Target compile => _ => _
        .DependsOn(verifyFormat)
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(_solution)
            .SetConfiguration(_configuration)
            .SetProperty("InformationalVersion", releaseTag)
            .EnableNoRestore()));

    private Target verifyFormat => _ => _
        .DependsOn(restore, verifyCommitMessages)
        .Executes(() =>
        {
            DotNetBuild(s => s.SetProjectFile(_solution).SetConfiguration(Configuration.Debug));

            DotNetFormat(s => s.SetProject(_solution).EnableVerifyNoChanges());
            DotNetFormat(s => s.SetProject(RootDirectory / "build" / "App.Build.csproj").EnableVerifyNoChanges());
        });

    private Target test => _ => _
        .DependsOn(compile)
        .Executes(() => DotNetTest(s => s
            .SetProjectFile(_solution)
            .SetConfiguration(_configuration)
            .EnableNoBuild()));

    private Target packInstaller => _ => _
        .DependsOn(test)
        .Executes(() =>
        {
            artifactsDirectory.CreateOrCleanDirectory();

            DotNetPublish(s => s
                .SetProject(RootDirectory / "src" / "App.Client.Windows" / "App.Client.Windows.csproj")
                .SetConfiguration(_configuration)
                .SetRuntime("win-x64")
                .DisableSelfContained()
                .SetOutput(publishDirectory)
                .SetVersion(packageVersion)
                .SetAssemblyVersion(packageVersion)
                .SetFileVersion(packageVersion)
                .SetInformationalVersion(releaseTag)
                .SetCopyright($"Copyright © {DateTime.UtcNow.Year} Your Company")
                .SetProperty("Company", "Your Company")
                .SetProperty("Product", "App.Client.Windows Sample")
                .SetProperty("AssemblyTitle", "App.Client.Windows Sample")
                .SetProperty("InnoSetupDotnetStarterIsPrereleaseBuild", _isPrereleaseBuild ? "true" : "false"));

            DotNetPublish(s => s
                .SetProject(RootDirectory / "installer" / "App.Installer.Tool" / "App.Installer.Tool.csproj")
                .SetConfiguration(_configuration)
                .SetRuntime("win-x64")
                .DisableSelfContained()
                .SetOutput(toolPublishDirectory)
                .SetVersion(packageVersion)
                .SetAssemblyVersion(packageVersion)
                .SetFileVersion(packageVersion)
                .SetInformationalVersion(releaseTag)
                .SetCopyright($"Copyright © {DateTime.UtcNow.Year} Your Company")
                .SetProperty("Company", "Your Company")
                .SetProperty("Product", "App.Client.Windows Sample")
                .SetProperty("AssemblyTitle", "App.Installer.Tool"));

            var nugetPackages = GetVariable("NUGET_PACKAGES")
                ?? Path.Combine(GetVariable("USERPROFILE") ?? GetVariable("HOME"), ".nuget", "packages");

            var isccExe = Directory.GetFiles(nugetPackages, "ISCC.exe", SearchOption.AllDirectories)
                .FirstOrDefault(x => x.Contains("tools.innosetup", StringComparison.OrdinalIgnoreCase));

            if (isccExe == null)
            {
                throw new InvalidOperationException(
                    "ISCC.exe not found under the NuGet packages folder after restore - was Tools.InnoSetup restored?");
            }

            var arguments = string.Join(" ",
                $"/DAppVersion=\"{packageVersion}\"",
                $"/DSourceDir=\"{publishDirectory}\"",
                $"/DToolSourceDir=\"{toolPublishDirectory}\"",
                $"/DOutputDir=\"{artifactsDirectory}\"",
                $"\"{setupScript}\"");

            using (var process = Process.Start(new ProcessStartInfo(isccExe, arguments) { UseShellExecute = false }))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"'ISCC.exe {arguments}' failed with exit code {process.ExitCode}");
                }
            }
        });

    private Target publishRelease => _ => _
        .DependsOn(packInstaller)
        .Requires(() => _gitHubToken)
        .Executes(async () =>
        {
            if (!shouldPublishRelease)
            {
                Log.Information(
                    "Build and tests passed, but skipping the actual release ({Reason}).",
                    nextRelease.Bump == VersionBump.None
                        ? "nothing release-worthy (feat/fix/BREAKING CHANGE) since the last release"
                        : "plain push to master - waiting for the next train or a manual dispatch");
                return;
            }

            GitHubTasks.GitHubClient.Credentials = new Credentials(_gitHubToken);

            var tag = releaseTag;
            var release = await GitHubTasks.GitHubClient.Repository.Release.Create(
                gitHubOwner,
                gitHubRepoName,
                new NewRelease(tag)
                {
                    Name = tag,
                    TargetCommitish = GitHubActions.Instance.Sha,
                    Prerelease = _isPrereleaseBuild,
                    Body = $"Automated build from commit {GitHubActions.Instance.Sha}."
                });

            foreach (var file in artifactsDirectory.GlobFiles("*.exe"))
            {
                await using (var stream = File.OpenRead(file))
                {
                    await GitHubTasks.GitHubClient.Repository.Release.UploadAsset(
                        release,
                        new ReleaseAssetUpload
                        {
                            FileName = file.Name,
                            ContentType = "application/octet-stream",
                            RawData = stream
                        }
                    );
                }
            }
        });
}