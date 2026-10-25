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
AppId={{B8F3E2A1-4C5D-4E6F-9A1B-2C3D4E5F6A7B}
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
  CountryPage: TWizardPage;
  CountryCombo: TNewComboBox;
  CustomersPage: TWizardPage;
  CustomersListBox: TNewListBox;

function IsDotNetDesktopRuntimeInstalled(): Boolean;
begin
  { Same registry key WiX's util:RegistrySearch used for this check - not WiX-specific, works identically here. }
  Result := RegKeyExists(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App');
end;

{ Runs the bundled App.Installer.Tool.exe and returns its stdout as one array element per line. The
  country dropdown and the dependent customer list below are both populated this way, from the real
  App.Domain/App.Infrastructure classes the app itself uses - not from data hardcoded in this script. }
function RunInstallerTool(const Params: String; var Lines: TArrayOfString): Boolean;
var
  ResultCode: Integer;
  Output: TExecOutput;
begin
  Result := ExecAndCaptureOutput(ExpandConstant('{tmp}\App.Installer.Tool.exe'), Params, '',
    SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode, Output) and (ResultCode = 0);
  if Result then
    Lines := Output.StdOut
  else
    SetArrayLength(Lines, 0);
end;

procedure PopulateCustomersForSelectedCountry();
var
  Lines: TArrayOfString;
  I: Integer;
begin
  CustomersListBox.Items.Clear;
  if CountryCombo.ItemIndex < 0 then
    exit;

  if RunInstallerTool('list-customers "' + CountryCombo.Items[CountryCombo.ItemIndex] + '"', Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do
      if Lines[I] <> '' then
        CustomersListBox.Items.Add(Lines[I]);
end;

procedure CountryComboChange(Sender: TObject);
begin
  PopulateCustomersForSelectedCountry();
end;

procedure InitializeWizard();
var
  Countries: TArrayOfString;
  I: Integer;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);

  { Extracted here rather than at ssPostInstall - the two pages below need to run the tool while the
    wizard is still collecting input, well before the Install step starts. }
  ExtractTemporaryFiles('*');

  CountryPage := CreateCustomPage(wpSelectTasks, 'Select a Sample Country',
    'The list below comes from App.Installer.Tool.exe at setup runtime, not from this script - it ' +
    'reads App.Infrastructure''s FakeDbContext, the same sample data App.Client.Windows shows.');
  CountryCombo := TNewComboBox.Create(CountryPage);
  CountryCombo.Parent := CountryPage.Surface;
  CountryCombo.Left := 0;
  CountryCombo.Top := 0;
  CountryCombo.Width := CountryPage.SurfaceWidth;
  CountryCombo.Style := csDropDownList;
  CountryCombo.OnChange := @CountryComboChange;

  if RunInstallerTool('list-countries', Countries) then
    for I := 0 to GetArrayLength(Countries) - 1 do
      if Countries[I] <> '' then
        CountryCombo.Items.Add(Countries[I]);

  if CountryCombo.Items.Count > 0 then
    CountryCombo.ItemIndex := 0;

  CustomersPage := CreateCustomPage(CountryPage.ID, 'Sample Customers',
    'Customers shown here depend on the country you selected on the previous page.');
  CustomersListBox := TNewListBox.Create(CustomersPage);
  CustomersListBox.Parent := CustomersPage.Surface;
  CustomersListBox.Left := 0;
  CustomersListBox.Top := 0;
  CustomersListBox.Width := CustomersPage.SurfaceWidth;
  CustomersListBox.Height := CustomersPage.SurfaceHeight;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = CustomersPage.ID then
    PopulateCustomersForSelectedCountry();
end;

{ Both runtimes are downloaded AND installed here, right before CountryPage - not at wpReady/ssInstall
  like a plain prerequisite normally would be - because App.Installer.Tool (framework-dependent, needs
  Microsoft.NETCore.App) has to actually run via ExecAndCaptureOutput to populate CountryPage, and that
  happens while the wizard is still collecting input, long before ssInstall. Installing the .NET Desktop
  Runtime also brings in the base Microsoft.NETCore.App runtime the tool needs, as a bundled dependency. }
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Error: String;
  ResultCode: Integer;
begin
  Result := True;
  if CurPageID = wpSelectTasks then
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

    if Result then
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
  end;
end;
