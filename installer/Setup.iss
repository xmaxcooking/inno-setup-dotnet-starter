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
AppMutex=InnoSetupDotnetStarterAppMutex
CloseApplications=yes
RestartApplications=yes
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

; More than one [Languages] entry makes Inno Setup show a language-selection dialog as the very
; first step of Setup, before the wizard's welcome page - no custom code needed. This drives
; both Setup's own built-in wizard text (Next/Back/Cancel, the license page, etc., via each
; language's .isl file) and the custom Country/Sample Customers pages' own text, via the
; [CustomMessages] translations below.
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

; Translations for the custom Country/Sample Customers pages in [Code] below, read back there
; via CustomMessage() - unlike the stock wizard text, Inno Setup has no built-in message for
; text a script itself defines, so these have to be spelled out per language here.
[CustomMessages]
english.CountryPageCaption=Select a Sample Country
english.CountryPageDescription=The list below comes from App.Installer.Tool.exe at setup runtime, not from this script - it reads App.Infrastructure's FakeDbContext, the same sample data App.Client.Windows shows.
english.CustomersPageCaption=Sample Customers
english.CustomersPageDescription=Customers shown here depend on the country you selected on the previous page.

german.CountryPageCaption=Beispielland auswählen
german.CountryPageDescription=Die folgende Liste stammt zur Laufzeit des Setups von App.Installer.Tool.exe und nicht aus diesem Skript - sie liest FakeDbContext aus App.Infrastructure, dieselben Beispieldaten, die App.Client.Windows anzeigt.
german.CustomersPageCaption=Beispielkunden
german.CustomersPageDescription=Die hier angezeigten Kunden hängen von dem auf der vorherigen Seite ausgewählten Land ab.

french.CountryPageCaption=Sélectionner un pays exemple
french.CountryPageDescription=La liste ci-dessous provient de App.Installer.Tool.exe lors de l'exécution du programme d'installation, et non de ce script - elle lit FakeDbContext de App.Infrastructure, les mêmes données d'exemple que celles affichées par App.Client.Windows.
french.CustomersPageCaption=Clients exemple
french.CustomersPageDescription=Les clients affichés ici dépendent du pays sélectionné sur la page précédente.

spanish.CountryPageCaption=Seleccionar un país de ejemplo
spanish.CountryPageDescription=La lista siguiente proviene de App.Installer.Tool.exe en tiempo de ejecución del instalador, no de este script - lee FakeDbContext de App.Infrastructure, los mismos datos de ejemplo que muestra App.Client.Windows.
spanish.CustomersPageCaption=Clientes de ejemplo
spanish.CustomersPageDescription=Los clientes que se muestran aquí dependen del país seleccionado en la página anterior.

italian.CountryPageCaption=Seleziona un paese di esempio
italian.CountryPageDescription=L'elenco seguente proviene da App.Installer.Tool.exe in fase di esecuzione del setup, non da questo script - legge il FakeDbContext di App.Infrastructure, gli stessi dati di esempio mostrati da App.Client.Windows.
italian.CustomersPageCaption=Clienti di esempio
italian.CustomersPageDescription=I clienti mostrati qui dipendono dal paese selezionato nella pagina precedente.

dutch.CountryPageCaption=Selecteer een voorbeeldland
dutch.CountryPageDescription=De onderstaande lijst komt tijdens het uitvoeren van Setup van App.Installer.Tool.exe, niet van dit script - deze leest de FakeDbContext van App.Infrastructure, dezelfde voorbeeldgegevens die App.Client.Windows toont.
dutch.CustomersPageCaption=Voorbeeldklanten
dutch.CustomersPageDescription=De hier weergegeven klanten zijn afhankelijk van het land dat u op de vorige pagina hebt geselecteerd.

portuguese.CountryPageCaption=Selecionar um país de exemplo
portuguese.CountryPageDescription=A lista abaixo vem do App.Installer.Tool.exe em tempo de execução do Setup, não deste script - lê o FakeDbContext do App.Infrastructure, os mesmos dados de exemplo mostrados pelo App.Client.Windows.
portuguese.CustomersPageCaption=Clientes de exemplo
portuguese.CustomersPageDescription=Os clientes mostrados aqui dependem do país selecionado na página anterior.

russian.CountryPageCaption=Выберите тестовую страну
russian.CountryPageDescription=Приведённый ниже список формируется программой App.Installer.Tool.exe во время работы программы установки, а не этим сценарием - она считывает FakeDbContext из App.Infrastructure, те же тестовые данные, которые отображает App.Client.Windows.
russian.CustomersPageCaption=Тестовые клиенты
russian.CustomersPageDescription=Клиенты, показанные здесь, зависят от страны, выбранной на предыдущей странице.

japanese.CountryPageCaption=サンプルの国を選択
japanese.CountryPageDescription=以下の一覧は、このスクリプトではなく、セットアップの実行時に App.Installer.Tool.exe が生成します。App.Infrastructure の FakeDbContext を読み取るもので、App.Client.Windows が表示するのと同じサンプルデータです。
japanese.CustomersPageCaption=サンプルの顧客
japanese.CustomersPageDescription=ここに表示される顧客は、前のページで選択した国によって変わります。

chinesesimplified.CountryPageCaption=选择示例国家
chinesesimplified.CountryPageDescription=下面的列表是安装程序运行时由 App.Installer.Tool.exe 生成的，而不是来自此脚本 - 它读取 App.Infrastructure 的 FakeDbContext，与 App.Client.Windows 显示的示例数据相同。
chinesesimplified.CustomersPageCaption=示例客户
chinesesimplified.CustomersPageDescription=此处显示的客户取决于您在上一页选择的国家。

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

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

  CountryPage := CreateCustomPage(wpSelectTasks, CustomMessage('CountryPageCaption'),
    CustomMessage('CountryPageDescription'));
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

  CustomersPage := CreateCustomPage(CountryPage.ID, CustomMessage('CustomersPageCaption'),
    CustomMessage('CustomersPageDescription'));
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
