#define MyAppName "LidDock"
#define MyAppVersion "1.0.1-DEV-1"
#define MyAppPublisher "MeowIce"
#define MyAppURL "https://github.com/MeowIce/LidDock"
#define MyAppExeName "LidDock.exe"

[Setup]
AppId={{8B237C5E-2A47-49D6-B785-3CF9D6318E29}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\assets\LidDock.ico
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=..\publish
OutputBaseFilename=LidDock-Setup
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Launch LidDock automatically on Windows startup"; GroupDescription: "Startup Options:"; Flags: checkedonce

[Files]
Source: "..\publish\staging\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\staging\LidDock.UI.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\LidDock.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\LidDock.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\LidDock.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\LidDock"; ValueType: dword; ValueName: "StartWithWindows"; ValueData: "1"; Tasks: autostart
Root: HKCU; Subkey: "Software\LidDock"; ValueType: dword; ValueName: "StartWithWindows"; ValueData: "0"; Tasks: not autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#MyAppName}"; Flags: deletevalue; Tasks: not autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall --silent"; Flags: runhidden; RunOnceId: "LidDockCleanUninstall"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\LidDock"
Type: filesandordirs; Name: "{userappdata}\LidDock"

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM LidDock.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM LidDock.UI.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM LidDock.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM LidDock.UI.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
