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

    // Bump Major for a breaking change, Minor for a new feature - commit the change on master
    // and the next train (or a manual dispatch) picks it up. A hotfix branch keeps whatever was
    // committed here at the tag it was cut from, so it keeps extending that older line's patch
    // sequence independently of whatever master has moved on to.
    private const int VersionMajor = 1;
    private const int VersionMinor = 0;

    // A version below 1.0.0 has made no compatibility promise yet, so it's automatically a
    // GitHub prerelease - not a manual per-build decision like it used to be, since that was
    // just as forgettable as any other "remember to flag this" rule.
    private bool isPrereleaseBuild => VersionMajor < 1;

    private Lazy<string> _releaseTagLazy;

    private string releaseTag
    {
        get
        {
            _releaseTagLazy ??= new Lazy<string>(computeReleaseTag);
            return _releaseTagLazy.Value;
        }
    }

    // A plain push to master always waits for the next train or a manual dispatch instead of
    // releasing immediately, so pushing to master repeatedly doesn't spam users with update
    // prompts; a hotfix branch releases right away. The weekday train additionally skips if
    // master's HEAD was already released (e.g. no commits landed since the last run), so it
    // doesn't cut a no-op release re-tagging identical bits under a new patch number.
    private bool shouldPublishRelease
    {
        get
        {
            if (GitHubActions.Instance is not { } gitHubActions)
            {
                return true;
            }

            if (gitHubActions.EventName == "push" && gitHubActions.RefName == "master")
            {
                return false;
            }

            if (gitHubActions.EventName == "schedule" && existingReleases().Any(r => r.TargetCommitish == gitHubActions.Sha))
            {
                return false;
            }

            return true;
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

    private string computeReleaseTag()
    {
        if (GitHubActions.Instance is null)
        {
            return SemanticVersion.Format(VersionMajor, VersionMinor, 0);
        }

        var prefix = $"v{VersionMajor}.{VersionMinor}.";

        var nextPatch = existingReleases()
            .Select(r => r.TagName)
            .Where(tag => tag.StartsWith(prefix, StringComparison.Ordinal))
            .Select(tag => int.TryParse(tag[prefix.Length..], out var patch) ? patch : -1)
            .Where(patch => patch >= 0)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        return $"{prefix}{nextPatch}";
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
        .DependsOn(restore)
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
                .SetProperty("InnoSetupDotnetStarterIsPrereleaseBuild", isPrereleaseBuild ? "true" : "false"));

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
                    "Build and tests passed, but skipping the actual release - plain push to master "
                    + "waits for the next train or a manual dispatch; the weekday train also skips if "
                    + "master's HEAD was already released.");
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
                    Prerelease = isPrereleaseBuild,
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