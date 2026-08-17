; Inno Setup script for DeskPet for Windows.
; Build:  ISCC.exe DeskPet.iss
; Produces: dist\DeskPet-setup-1.0.1.exe  (online bootstrap installer)

#define MyAppName "DeskPet"
#define MyAppNameShort "DeskPet"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "BuyMyCheatPlz"
#define MyAppURL "https://github.com/BuyMyCheatPlz/DeskPet"
#define MyAppExeName "DeskPet.exe"

[Setup]
AppId={{8E2F3A6C-1D7E-4B2A-9C6F-4D9A0B7C5E21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppNameShort}
DefaultGroupName={#MyAppNameShort}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=DeskPet-setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "开机自动启动 DeskPet"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\Skins\*"; DestDir: "{app}\Skins"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppNameShort}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppNameShort}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional autostart via HKCU (no admin needed, matches app's own AutoStart).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DeskPet"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// ---- .NET 8 Desktop Runtime detection ----
// Parse a dotted version string into an integer "major * 1e6 + minor * 1e3 + patch"
// so we can compare >= 8.0.0 without relying on a built-in StrToVersion.
function VersionToInt(S: String): LongInt;
var
  P, D1, D2: Integer;
begin
  Result := 0;
  P := Pos('.', S);
  if P > 0 then
  begin
    Result := Result + StrToInt(Copy(S, 1, P - 1)) * 1000000;
    S := Copy(S, P + 1, Length(S));
    P := Pos('.', S);
    if P > 0 then
    begin
      Result := Result + StrToInt(Copy(S, 1, P - 1)) * 1000;
      S := Copy(S, P + 1, Length(S));
    end;
  end;
  // remaining tokens may be build strings like "8.0.17" -> first two used; ignore rest
  D1 := Pos('.', S);
  if D1 > 0 then S := Copy(S, 1, D1 - 1);
  D2 := Pos('-', S);
  if D2 > 0 then S := Copy(S, 1, D2 - 1);
  if S <> '' then
    Result := Result + StrToIntDef(S, 0);
end;

function IsDotNetDesktopRuntimeInstalled(): Boolean;
var
  val: String;
begin
  // 64-bit view of HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App
  Result := False;
  if RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', 'Version', val) then
    if VersionToInt(val) >= 8000000 then   // >= 8.0.0
      Result := True;
end;

procedure InitializeWizard();
var
  Msg, DotNetUrl: String;
  ErrorCode: Integer;
begin
  if IsDotNetDesktopRuntimeInstalled() then Exit;

  Msg :=
    '未检测到 .NET 8 Desktop Runtime。' + #13#10 + #13#10 +
    'DeskPet 需要它才能运行（如果你已安装最新版 .NET 8/9 Desktop Runtime 也可能满足）。' + #13#10 + #13#10 +
    '请先安装后再继续：' + #13#10 +
    '   方法一：打开"设置"或商店的 .NET 8 Desktop Runtime 页面；' + #13#10 +
    '   方法二：在 PowerShell 运行  winget install Microsoft.DotNet.DesktopRuntime.8。' + #13#10 + #13#10 +
    '点"确定"将打开官方下载页（.NET 8 Desktop Runtime x64）。安装完成后点"取消"退出本向导并重新运行。';

  if MsgBox(Msg, mbInformation, MB_OKCANCEL or MB_DEFBUTTON1) = IDOK then
  begin
    DotNetUrl := 'https://dotnet.microsoft.com/download/dotnet/8.0';
    if not ShellExec('open', DotNetUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode) then
      MsgBox('无法打开浏览器：' + SysErrorMessage(ErrorCode), mbError, MB_OK);
  end;
  // Abort installation unless runtime present (checked again below).
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpWelcome) and not IsDotNetDesktopRuntimeInstalled() then
  begin
    if MsgBox(
       '仍然需要 .NET 8 Desktop Runtime。是否就此取消安装？' + #13#10 +
       '（若你确定已安装，可点"否"继续。）',
       mbConfirmation, MB_YESNO) = IDYES then
      Result := False;
  end;
end;
