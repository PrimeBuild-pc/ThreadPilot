; ThreadPilot Installer Script for Inno Setup
; Copyright (C) 2025 Prime Build

#define MyAppName "ThreadPilot"
#define MyAppPublisher "Prime Build"
#define MyAppURL "https://github.com/PrimeBuild-pc/ThreadPilot"
#define MyAppExeName "ThreadPilot.exe"

#ifndef MyWizardStyle
	#define MyWizardStyle "modern dynamic windows11"
#endif

#ifndef MyAppVersion
	#define MyAppVersion "1.3.1"
#endif

#ifndef MyAppSourceDir
	#define MyAppSourceDir "..\\artifacts\\release\\singlefile"
#endif

[Setup]
AppId={{E8F7A3B2-5C4D-4E6F-8A9B-1C2D3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
PrivilegesRequired=admin
OutputDir=..\artifacts\release\installer
OutputBaseFilename=ThreadPilot_v{#MyAppVersion}_Setup
SetupIconFile=..\assets\icons\ico.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle={#MyWizardStyle}
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableReadyPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\ThreadPilot.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#MyAppSourceDir}\*.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#MyAppSourceDir}\Powerplans\*"; DestDir: "{app}\Powerplans"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Intentionally do not auto-launch after setup to keep package-manager installs unattended.
; ThreadPilot user data is preserved during install/update. Inno removes installed
; files and shortcuts automatically only when the generated uninstaller runs.
; Per-user AppData cleanup is limited to the account context used by uninstall.
[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/IM ""{#MyAppExeName}"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallKillThreadPilot"
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""ThreadPilot_Startup"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallRemoveThreadPilotStartupTask"
Filename: "reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""ThreadPilot"" /f"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallRemoveThreadPilotRunEntry"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\ThreadPilot"

[Code]
const
  LegacyBetaUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{A2A4C8B5-4A9A-4B1B-93F4-5F8B1C7E8C2A}_is1';
  LegacyBetaDisplayName = 'ThreadPilot 0.1.0-beta';

function IsLegacyThreadPilotInstallPath(InstallLocation: string): Boolean;
var
  NormalizedLocation: string;
  ExpectedInstallRoot: string;
begin
  NormalizedLocation := Lowercase(RemoveBackslashUnlessRoot(RemoveQuotes(InstallLocation)));
  ExpectedInstallRoot := Lowercase(RemoveBackslashUnlessRoot(ExpandConstant('{autopf}\ThreadPilot')));
  Result := (NormalizedLocation = ExpectedInstallRoot);
end;

procedure DeleteLegacyBetaUninstallEntry(RootKey: Integer);
var
  DisplayName: string;
  InstallLocation: string;
begin
  if RegQueryStringValue(RootKey, LegacyBetaUninstallKey, 'DisplayName', DisplayName) and
     RegQueryStringValue(RootKey, LegacyBetaUninstallKey, 'InstallLocation', InstallLocation) and
     (DisplayName = LegacyBetaDisplayName) and
     IsLegacyThreadPilotInstallPath(InstallLocation) then
  begin
    RegDeleteKeyIncludingSubkeys(RootKey, LegacyBetaUninstallKey);
  end;
end;

function InitializeSetup(): Boolean;
begin
  DeleteLegacyBetaUninstallEntry(HKLM);
  DeleteLegacyBetaUninstallEntry(HKCU);
  Result := True;
end;
