; ThreadPilot installer script (Inno Setup)
; Requires Inno Setup to build the installer. Adjust MyAppVersion/MyAppBuildDir as needed.

#define MyAppName "ThreadPilot"
#define MyAppPublisher "ThreadPilot"
#define MyAppURL "https://github.com/"
#define MyAppExeName "ThreadPilot.exe"
#define MyAppVersion "1.0.0"
; Point this to the folder containing the published binaries (e.g. dotnet publish -c Release -r win-x64)
#define MyAppBuildDir "..\\artifacts\\build"

[Setup]
AppId={{A2A4C8B5-4A9A-4B1B-93F4-5F8B1C7E8C2A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequiredOverridesAllowed=dialog
OutputBaseFilename=ThreadPilot_Setup
SetupIconFile=..\ico.ico
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppBuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; BeforeInstall: KillRunningInstance()

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: taskkill.exe; Parameters: "/IM '{#MyAppExeName}' /F"; Flags: runhidden waituntilterminated; RunOnceId: UninstallKill

[Code]

procedure KillRunningInstance();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/IM "{#MyAppExeName}" /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
