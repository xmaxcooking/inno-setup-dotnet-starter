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
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static Fallout.Common.EnvironmentInfo;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// [GitHubActions] generates .github/workflows/release.yml from the attribute below; never hand-edit the .yml.
[GitHubActions(
    "release",
    GitHubActionsImage.WindowsLatest,
    OnPushBranches = new[] { "master" },
    InvokedTargets = new[] { nameof(publishRelease) },
    EnableGitHubToken = true,
    WritePermissions = new[] { GitHubActionsPermissions.Contents },
    PublishArtifacts = false,
    ConcurrencyGroup = "release",
    ConcurrencyCancelInProgress = false)]
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

    private string releaseTag => SemanticVersion.Format(GitHubActions.Instance?.RunNumber ?? 0);

    private string packageVersion => GitHubActions.Instance is { } gitHubActions
        ? $"1.0.{gitHubActions.RunNumber}.0"
        : $"1.0.{(DateTime.UtcNow.Date - new DateTime(2000, 1, 1)).Days}.{(int)(DateTime.UtcNow.TimeOfDay.TotalSeconds / 2)}";

    private string gitHubOwner => _gitRepository.GetGitHubOwner();
    private string gitHubRepoName => _gitRepository.GetGitHubName();

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