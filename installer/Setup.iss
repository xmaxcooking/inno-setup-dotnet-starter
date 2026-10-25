#ifndef AppVersion
  #define AppVersion "0.0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\src\App.Client.Windows\bin\Release\net10.0-windows\win-x64\publish"
#endif
#ifndef ToolSourceDir
  #define ToolSourceDir "..\installer\App.Installer.Tool\bin\Release\net10.0\win-x64\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{B8F3E2A1-4C5D-4E6F-9A1B-2C3D4E5F6A7B}}
AppName=App.Client.Windows Sample
AppVersion={#AppVersion}
AppPublisher=Your Company
DefaultDirName={autopf}\App.Client.Windows Sample
DefaultGroupName=App.Client.Windows Sample
UninstallDisplayIcon={app}\App.Client.Windows.exe
SetupIconFile=..\assets\icon.ico
LicenseFile=..\assets\License.txt
WizardStyle=modern
WizardSmallImageFile=..\assets\logo.bmp
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
OutputDir={#OutputDir}
OutputBaseFilename=AppClientWindowsSample-Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion; Excludes: "*.pdb"
Source: "{#ToolSourceDir}\*"; DestDir: "{tmp}"; Flags: dontcopy recursesubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\App.Client.Windows Sample"; Filename: "{app}\App.Client.Windows.exe"
Name: "{autodesktop}\App.Client.Windows Sample"; Filename: "{app}\App.Client.Windows.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\App.Client.Windows.exe"; Description: "{cm:LaunchProgram,App.Client.Windows Sample}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;

function IsDotNetDesktopRuntimeInstalled(): Boolean;
begin
  { Same registry key WiX's util:RegistrySearch used for this check - not WiX-specific, works identically here. }
  Result := RegKeyExists(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App');
end;

procedure InitializeWizard();
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Error: String;
begin
  Result := True;
  if CurPageID = wpReady then
  begin
    DownloadPage.Clear;

    if not IsDotNetDesktopRuntimeInstalled() then
    begin
      { URL/hash verified live this session: HTTP 200, Content-Length 60032984, SHA256 computed from the actual downloaded file. }
      DownloadPage.Add(
        'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.12/windowsdesktop-runtime-10.0.12-win-x64.exe',
        'windowsdesktop-runtime-win-x64.exe',
        '55a67d8476cde95a9cc43a95803b4f54446e7c9251ed22aa6421d4908174ae84');
    end;

    { Windows App Runtime has no DetectCondition at all, mirroring the WiX repo's WindowsAppRuntime.wxs:
      the runtime installer itself is idempotent/quiet, so this is a deliberate, documented choice to
      always attempt the install rather than an oversight.
      URL/size/hash verified live this session: HTTP 200 behind the aka.ms redirect, Content-Length
      120199480, SHA256 computed from the actual downloaded file. }
    DownloadPage.Add(
      'https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-x64.exe',
      'windowsappruntimeinstall-x64.exe',
      '931a421e8dc3e6e67724806cb67fecdbb88dfe323f0170842eb4a4b4b149f1e2');

    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        if DownloadPage.AbortedByUser then
          Log('Runtime download aborted by user.')
        else begin
          Error := Format('%s: %s', [DownloadPage.LastBaseNameOrUrl, GetExceptionMessage]);
          SuppressibleMsgBox(AddPeriod(Error), mbCriticalError, MB_OK, IDOK);
          Result := False;
        end;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  SampleParam: String;
begin
  if CurStep = ssInstall then
  begin
    if not IsDotNetDesktopRuntimeInstalled() then
    begin
      if not Exec(ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe'), '/install /quiet /norestart', '',
          SW_SHOW, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
        Log('.NET Desktop Runtime install failed (exit code ' + IntToStr(ResultCode) + ').');
    end;

    { No DetectCondition for this one - always run it, same as the WiX repo's WindowsAppRuntime.wxs. }
    if not Exec(ExpandConstant('{tmp}\windowsappruntimeinstall-x64.exe'), '--quiet', '',
        SW_SHOW, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
      Log('Windows App Runtime install failed (exit code ' + IntToStr(ResultCode) + ').');
  end;

  if CurStep = ssPostInstall then
  begin
    { ExtractTemporaryFiles (plural, confirmed in jrsoftware/issrc source) extracts every dontcopy-flagged
      temp file matching the pattern in one call - the App.Installer.Tool publish output has an unknown/
      variable number of dependency files, so this avoids hardcoding each one. }
    ExtractTemporaryFiles('*');
    SampleParam := ExpandConstant('{param:SAMPLEPARAM|}');
    Exec(ExpandConstant('{tmp}\App.Installer.Tool.exe'), '"' + SampleParam + '"', '',
      SW_HIDE, ewWaitUntilTerminated, ResultCode);

    { ValidateSampleProperty/WriteSampleMarkerFile equivalent from the WiX repo's MSI custom actions -
      demonstrates install-time Pascal Script logic, with none of MSI's net48/mscoree constraint. }
    SaveStringToFile(ExpandConstant('{app}\installed-by-setup.txt'),
      'Installed ' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', #0, #0) + #13#10, False);
  end;
end;
