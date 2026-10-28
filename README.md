# inno-setup-dotnet-starter

Template repository for .NET 10 projects packaged with an Inno Setup installer. Sibling to
[wix-dotnet-starter](https://github.com/xmaxcooking/wix-dotnet-starter) - see "Why Inno Setup
instead of WiX?" below for why this one exists.

## Layout

```
InnoSetupDotnetStarter.slnx
Directory.Packages.props  central package management - every PackageReference's version lives
                    here; project files reference packages with no Version attribute
assets/            icon/logo/license assets used by the installer
src/
  App.Domain/      class library - the "Customer" domain model
  App.Abstractions/ class library - cross-cutting interfaces (IFileSystem, IProcessLauncher,
                    ISystemDateTime) - implementation detail behind App.Core.Api by convention,
                    not enforced by the compiler (see "App.Core.Api" below)
  App.Infrastructure/ class library - data access abstraction (IAppDbContext) + concrete data
                    access (FakeDbContext, in-memory sample data) - same convention
  App.Updates/      class library - update-check feed, settings, and client identity - same
                    convention
  App.Core.Api/     class library - the app's real public surface. App.Client.Windows and
                    App.Installer.Tool are only allowed to reference this, not the three projects
                    above directly - see "App.Core.Api: the app's real public surface" below
  App.Client.Windows/  WinUI 3 app (net10.0-windows10.0.19041.0) - entry point, shows the sample
                    data in a ListView, Settings window for the update channel
tests/
  App.Infrastructure.Tests/ MSTest tests for App.Infrastructure
  App.Updates.Tests/ MSTest tests for App.Updates
installer/
  Setup.iss        the whole installer - an Inno Setup script, not an MSBuild project; compiled
                    by ISCC.exe (see "Build the installer only" below)
  App.Installer.Tool/  .NET 10 console app, bundled into the installer and run via
                    ExecAndCaptureOutput() to populate two custom wizard pages (a country dropdown,
                    then a dependent customer list) straight from App.Core.Api's real sample
                    data - not from data hardcoded in the script. None of MSI's net48/mscoree
                    constraint on install-time code either (see "Adapting this template" below)
build/
  App.Build.csproj Fallout (NUKE successor) build pipeline - see "Full pipeline" below
```

## `App.Core.Api`: the app's real public surface

`App.Client.Windows` and `App.Installer.Tool` only ever reference `App.Core.Api` - never
`App.Domain`/`App.Infrastructure`/`App.Updates`/`App.Abstractions` directly. Both call
`services.AddCoreApi()` and resolve `ICustomerDirectory`/`IUpdateService`/`IUpdateChannelSettings`
from DI, never anything from the four underlying projects by name.

This is a convention, not a compiler-enforced boundary - deliberately. `internal` +
`[assembly: InternalsVisibleTo(...)]` could enforce it (and was tried during this project's
history), but using `InternalsVisibleTo` to grant one specific *project* friend access to several
others, purely to simulate a module boundary .NET doesn't have natively, creates its own problems:
bare strings with no compile-time checking, and assemblies that export zero public types and so
look broken when opened in isolation. `InternalsVisibleTo` is still used here, just for its normal,
narrow job - granting each project's own test project access to its concrete implementation types
that only that project and its tests ever construct directly (`FakeDbContext`,
`GitHubReleaseUpdateFeed`, `ClientIdentity`, `UpdateSettings`, `AssemblyMetadataDefaultUpdateChannel`).
Everything `App.Core.Api` itself needs to reference - `IAppDbContext`, `UpdateCheck`, `IUpdateFeed`,
`IClientIdentity`, `IUpdateSettingsStore`, each project's `ServiceCollectionExtensions` - stays
`public`, same as any other normal multi-project .NET solution; "go through `App.Core.Api`" is
enforced by code review, the same way most architectural conventions are.

The payoff of the convention alone: a diff that only touches `App.Infrastructure`/`App.Updates`/
`App.Abstractions` is easy to recognize as internal-only by a reviewer, and a diff that touches
`App.Core.Api`'s own three interfaces is the one case actually worth asking "does this need a
`VersionMajor` bump" - without needing the compiler to prove it. If you extend this template with a
genuinely independent consumer of `App.Core.Api` (e.g. a published NuGet package, or a UI that
isn't rebuilt from the same solution every time), that's the point where relying on review stops
being enough and an automated API-compatibility check (e.g. `Microsoft.DotNet.ApiCompat`) would
earn its keep - a forgotten `VersionMajor` bump stops being something a reviewer would always catch
by eye and starts being something a published consumer could silently get broken by. For two
consumers that are always rebuilt together in the same solution (the case today), the C# compiler
already catches that on every build, so neither `InternalsVisibleTo`-as-enforcement nor ApiCompat
would add real protection yet.

## Prerequisites

- .NET 10 SDK
- Windows, for the WinUI 3 app and for building/running the installer

## Build

```
dotnet build
```

Builds the class libraries, the WinUI 3 app, and the tests. This does **not** build the
installer - `installer/Setup.iss` isn't an MSBuild project, so `dotnet build` has nothing to do
with it. See "Build the installer only" below.

## Run the sample app

```
dotnet run --project src/App.Client.Windows
```

## Run tests

```
dotnet test
```

## Build the installer only

```
dotnet tool restore
dotnet fallout packInstaller
```

Produces `artifacts/AppClientWindowsSample-Setup.exe`. Unlike WiX's `.wixproj` (an MSBuild SDK project `dotnet build`
can target directly), an Inno Setup `.iss` script has no MSBuild representation at all - it's
compiled by invoking `ISCC.exe` as an external process, which is what the `packInstaller` Fallout
target does (it publishes `App.Client.Windows` and `App.Installer.Tool`, then runs ISCC against
`installer/Setup.iss` with `/D` command-line defines for the paths/version).

## Full pipeline (Fallout)

`build/` is a [Fallout](https://fallout.build/) build (the maintained successor to NUKE - a
C#-first build automation tool: your pipeline is a real console app, not YAML). It showcases the
pipeline end to end - Clean, Restore, Compile, Test, and `PackInstaller`.

```
dotnet tool restore
dotnet fallout Test            # Restore -> Compile -> Test (the default if you just run `dotnet fallout`)
dotnet fallout PackInstaller   # -> also builds the installer
dotnet fallout Clean
```

`dotnet tool restore` pulls in the pinned `fallout.globaltool` from `.config/dotnet-tools.json`
so `dotnet fallout` works without a machine-wide install; `./build.sh` / `.\build.ps1` do the
same plus provision the .NET SDK itself if it's missing, which is what a CI runner would call.
`build/Build.cs` is the pipeline definition - add targets there the same way you'd add any other
C# method.

`build/Properties/launchSettings.json` has one profile per target (`Test`, `PackInstaller`, a
`PackInstaller (Release)` variant, ...), so you can run/debug any of them from an IDE's run
configuration dropdown instead of the command line.

## CI: release trains, plus hotfix branches

`.github/workflows/release.yml` and `release-manual.yml` are both generated from the two
`[GitHubActions]` attributes on `Build` in `build/Build.cs` - don't hand-edit the `.yml` files;
change the attributes and run the build once to regenerate them (two files because Fallout
doesn't allow mixing the shorthand `On` trigger list with the detailed `On*` properties in one
attribute). Both ultimately run `publishRelease` (which depends on `packInstaller`) on
`windows-latest`. Auth is `GITHUB_TOKEN`, provided automatically by Actions; no secrets to
configure. `global.json` pins the SDK version `actions/setup-dotnet` installs.

- **`master`** is the rolling "latest" line. Every push to it builds and tests, but does *not*
  publish a release on its own (`shouldPublishRelease` in `Build.cs`) - otherwise every commit to
  master would prompt every installed app to update, which gets old fast. A release only actually
  gets cut from master by:
  - the **weekday train**: `release.yml`'s `OnCronSchedule` (`0 3 * * 1-5`, UTC - Monday through
    Friday, so 5 times a week, not weekends) builds and publishes whatever is on master's HEAD at
    that time - but only if master's HEAD hasn't already been released (by an earlier train run or
    a hotfix), so the train doesn't cut a no-op release re-tagging identical bits on a day with no
    new commits;
  - a **manual dispatch**: run `release-manual.yml` from the Actions tab (or `gh workflow run
    release-manual.yml`) whenever a maintainer decides master is ready sooner than the next train.
- **`hotfix/*` branches** release immediately on every push, no waiting for a train - branch off
  an existing release's tag (e.g. `git checkout -b hotfix/1.0 v1.0.12`), cherry-pick the fix, push.

The version is tracked manually, deliberately: `VersionMajor`/`VersionMinor` are plain constants in
`Build.cs`, bumped by hand and committed like any other source change - a human decides when
something is a breaking change or a new feature, and that decision shows up as a normal, reviewable
diff. (An earlier version of this mechanism tried deriving the bump automatically from [Conventional
Commits](https://www.conventionalcommits.org/) message prefixes - `fix:`/`feat:`/`BREAKING CHANGE:`
- but that makes the version depend on a human remembering to self-report significance *in commit
message text*, at the moment they're focused on the change itself rather than its compatibility
impact. Forgetting the marker is a silent failure: nothing blocks it, it just ships mislabeled, with
no review-time visibility into the mistake. A manual constant doesn't fully solve "a human might
forget," but it at least turns the decision into a visible diff a reviewer can question, rather than
a sentence buried in commit body text that gets no scrutiny at all.)

PATCH still auto-increments per `Major.Minor` line: `computeReleaseTag()` scans existing release
tags for the exact `v{VersionMajor}.{VersionMinor}.` prefix and takes the highest existing patch
plus one. This is already hotfix-branch-correct with no extra bookkeeping - a `hotfix/1.0` branch
cut from `v1.0.12` has `VersionMajor`/`VersionMinor` = `1`/`0` baked into its own `Build.cs` by
construction (that's whatever was committed at the tag it was cut from), so it naturally keeps
extending `v1.0.*`'s patch sequence independently of whatever master's constants have moved on to.

Every release is a GitHub prerelease by design; there's no automated "promote to stable" step.
To promote a build so the Stable update channel (see `src/App.Updates/`) picks it up, open that
release on GitHub and uncheck "Set as a pre-release" - the app's Stable channel reads
`GET /releases/latest`, which only ever returns the most recent *non-prerelease* release. The
Beta channel instead fetches a page of recent releases and picks the highest version among them -
the list endpoint's order isn't reliably newest-first in practice, so position 0 can't be trusted.

## Why Inno Setup instead of WiX?

The sibling template [wix-dotnet-starter](https://github.com/xmaxcooking/wix-dotnet-starter) ran
into a real limitation while trying to add a second installer checkbox: WiX v4+ makes a duplicate
`Dialog`/`Control` symbol from a referenced library a hard linker error, so you can't override a
single stock dialog (like the MSI's `ExitDialog`) without forking WixUI's entire dialog set - and
even that template's existing desktop-shortcut checkbox never shows up through the real shipped
installer anyway, since Burn shows its own UI instead of the MSI's. Getting a real custom
checkbox in WiX requires writing a full custom Bootstrapper Application (a WPF app owning the
whole install UI) - disproportionate scope for a template.

Inno Setup has `Create a desktop icon` and `Launch application` as literal, built-in wizard
checkboxes - see `[Tasks]`/`[Icons]`/`[Run]` in `installer/Setup.iss`. It also has no equivalent
of MSI's net48/mscoree constraint on install-time code (`installer/App.Installer.Tool` is called
via `ExecAndCaptureOutput()` to drive two custom wizard pages, no Burn-ExePackage-style workaround
needed), and its compiler
(`ISCC.exe`, via the `Tools.InnoSetup` NuGet package - maintained by Jordan Russell, Inno Setup's
own author) has no license fee, unlike WiX's pre-built binaries from v6 onward.

What's genuinely lost: no MSI/Windows Installer format (matters if you need enterprise deployment
via Intune/SCCM/Group Policy), no equivalent of MSI's deferred/rollback-journaled custom
action model - Inno has its own uninstall-log-based rollback for files/registry, but nothing
generically transactional - and no MSI-style Modify/Repair/Remove maintenance page when you
re-run Setup against an existing install.

### Repairing or reinstalling

Inno Setup has no separate "repair" mode. Re-running `AppClientWindowsSample-Setup.exe` (whether
the user downloads it again or it's the update installer this app launches itself - see
`src/App.Updates/UpdateCheck.cs`) just runs the same install over the existing one: every file in
`[Files]` has `Flags: ignoreversion`, so it's always rewritten from the source payload regardless
of what version is already on disk, which is the repair.

The only thing that can get in the way of that is the app still running: Setup can't overwrite a
locked `App.Client.Windows.exe`. `AppMutex=InnoSetupDotnetStarterAppMutex` in `[Setup]`, matched by
a `Mutex` the app holds for its whole lifetime in `App.Client.Windows/App.xaml.cs`, makes Setup
detect that and prompt the user to close it rather than silently leaving the old files in place;
`CloseApplications=yes`/`RestartApplications=yes` additionally let Setup ask the Windows Restart
Manager to close and relaunch it automatically. The app's own auto-update path
(`UpdateCheck.DownloadAndLaunchAsync` + `App.xaml.cs`) also calls `Environment.Exit(0)` right
after launching the downloaded installer, a hard process exit rather than WinUI's graceful
`Application.Exit()`, so its own mutex and file locks are released before Setup gets to the
file-copy step instead of racing it.

### Multiple languages

`installer/Setup.iss`'s `[Languages]` section lists ten languages using the official `.isl`
translations that ship inside the `Tools.InnoSetup` NuGet package
(`tools/Languages/*.isl` next to `ISCC.exe`) - more are available there if you want them. Listing
more than one language is all it takes for Inno Setup to show a language-selection dialog as the
very first step of Setup, before the welcome page; no custom code is involved. That drives
Setup's own built-in wizard text (Next/Back/Cancel, the license page, and so on) automatically.

Text a script defines itself has no built-in translation, though: both the "Create a &desktop
icon" task (`{cm:CreateDesktopIcon}`/`{cm:AdditionalIcons}` - reusing a message every bundled
`.isl` already defines for exactly this, so it didn't need its own entry below) and the custom
"Select a Sample Country"/"Sample Customers" pages in `[Code]` (`CustomMessage('CountryPageCaption')`
etc.) are spelled out per language in the `[CustomMessages]` section above `[Tasks]`. The script
file itself needs a UTF-8 BOM for Inno Setup to read the Cyrillic/Japanese/Chinese text in there
correctly - without one, a script with no BOM is read using the system's ANSI code page instead,
which can't represent those characters.

## Adapting this template

- `installer/Setup.iss`'s `AppId` is a freshly generated placeholder GUID
  (`B8F3E2A1-4C5D-4E6F-9A1B-2C3D4E5F6A7B`) - regenerate it once for your own product and then
  never change it again after the first release; Inno Setup uses it the same way WiX uses
  `UpgradeCode` to detect upgrades of the same product.
- Replace the placeholder `AppPublisher`/`AppName` in `installer/Setup.iss`, and the
  `xmaxcooking`/`inno-setup-dotnet-starter` `AssemblyMetadata` values in
  `src/App.Client.Windows/App.Client.Windows.csproj` (read by `App.Updates` to know which GitHub
  repo to check for updates), before shipping anything built from this template.
- `installer/Setup.iss`'s `[Code]` section downloads and installs the Windows App Runtime and
  the .NET Desktop Runtime - keep this block if you keep the `Microsoft.WindowsAppSDK` package
  reference (WinUI), otherwise it's safe to delete along with that package reference if you ever
  drop WinUI entirely. `IsWindowsAppRuntimeInstalled` shells out to PowerShell's
  `Get-AppxPackage` to skip the ~120 MB download/install when a compatible runtime is already
  present - there's no registry-only equivalent, since Windows App Runtime is deployed as MSIX
  packages rather than a classic MSI/registry install.
- `App.Domain` is the seam for real domain models; `App.Infrastructure` holds both the data
  access abstraction (`IAppDbContext`) and its real implementation (e.g. a real EF Core
  `DbContext` in place of `FakeDbContext`) - `IAppDbContext` mirrors EF Core's own `DbContext`
  shape, so it's a persistence concern, not a domain one. `App.Core.Api` only ever depends on
  `IAppDbContext` (public); `FakeDbContext` itself is `internal` (visible only to
  `App.Infrastructure.Tests`, which constructs it directly) - swapping it for a real
  implementation needs no change to `App.Core.Api` or either consumer.
- `assets/logo.bmp` must stay a `.bmp` - Inno Setup's wizard image attributes don't accept PNG.
  `assets/License.txt` is plain text (Inno doesn't require RTF, unlike WiX's WixUI).
