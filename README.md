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
  App.Infrastructure/ class library - data access abstraction (IAppDbContext) + concrete data
                    access (FakeDbContext, in-memory sample data)
  App.Updates/      class library - update-check feed, settings, and client identity
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
                    then a dependent customer list) straight from App.Infrastructure's real sample
                    data - not from data hardcoded in the script. None of MSI's net48/mscoree
                    constraint on install-time code either (see "Adapting this template" below)
build/
  App.Build.csproj Fallout (NUKE successor) build pipeline - see "Full pipeline" below
```

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

## CI: release on push to master

`.github/workflows/release.yml` is generated from the `[GitHubActions]` attribute on `Build` in
`build/Build.cs` - don't hand-edit the `.yml`; change the attribute and run the build once to
regenerate it. On every push to `master` it runs `publishRelease` (which depends on
`packInstaller`) on `windows-latest`, creating a GitHub Release tagged `build-<run number>` -
always unique, no collisions across re-runs - marked as a prerelease, with
`AppClientWindowsSample-Setup.exe` attached.
Auth is `GITHUB_TOKEN`, provided automatically by Actions; no secrets to configure. `global.json`
pins the SDK version `actions/setup-dotnet` installs.

Every release is a GitHub prerelease by design; there's no automated "promote to stable" step.
To promote a build so the Stable update channel (see `src/App.Updates/`) picks it up, open that
release on GitHub and uncheck "Set as a pre-release" - the app's Stable channel reads
`GET /releases/latest`, which only ever returns the most recent *non-prerelease* release.

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
very first step of Setup, before the welcome page; no custom code is involved. That dialog, and
the translation it drives, only covers Setup's own built-in wizard text (Next/Back/Cancel, the
license page, and so on) - the custom "Select a Sample Country"/"Sample Customers" pages in
`[Code]` are plain Pascal Script strings and always display in English.

## Adapting this template

- `installer/Setup.iss`'s `AppId` is a freshly generated placeholder GUID
  (`B8F3E2A1-4C5D-4E6F-9A1B-2C3D4E5F6A7B`) - regenerate it once for your own product and then
  never change it again after the first release; Inno Setup uses it the same way WiX uses
  `UpgradeCode` to detect upgrades of the same product.
- Replace the placeholder `AppPublisher`/`AppName` in `installer/Setup.iss`, and the
  `xmaxcooking`/`inno-setup-dotnet-starter` `AssemblyMetadata` values in
  `src/App.Client.Windows/App.Client.Windows.csproj` (read by `App.Updates` to know which GitHub
  repo to check for updates), before shipping anything built from this template.
- `installer/Setup.iss`'s `[Code]` section always attempts the Windows App Runtime install with
  no detection check, mirroring a deliberate choice in the WiX sibling template (the installer
  itself is idempotent/quiet) - this app doesn't actually use the Windows App SDK/WinUI runtime
  beyond what's already chained for .NET Desktop Runtime's sibling concerns; if you add a real
  WinUI/Windows App SDK dependency, keep this block, otherwise it's safe to delete along with the
  `Microsoft.WindowsAppSDK` package reference if you ever drop WinUI entirely.
- `App.Domain` is the seam for real domain models; `App.Infrastructure` holds both the data
  access abstraction (`IAppDbContext`) and its real implementation (e.g. a real EF Core
  `DbContext` in place of `FakeDbContext`) - `IAppDbContext` mirrors EF Core's own `DbContext`
  shape, so it's a persistence concern, not a domain one.
- `assets/logo.bmp` must stay a `.bmp` - Inno Setup's wizard image attributes don't accept PNG.
  `assets/License.txt` is plain text (Inno doesn't require RTF, unlike WiX's WixUI).
