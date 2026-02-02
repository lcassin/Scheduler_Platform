; Inno Setup Script for Mermaid Editor
; This script creates a Windows installer for the Mermaid Editor application

#define MyAppName "Mermaid Editor"
#define MyAppVersion "1.2"
#define MyAppPublisher "Scheduler Platform"
#define MyAppURL "https://github.com/lcassin/Scheduler_Platform"
#define MyAppExeName "MermaidEditor.exe"
#define MyAppId "8F3E4A2B-1C5D-4E6F-9A8B-7C2D3E4F5A6B"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{{#MyAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings - adjust OutputDir as needed
OutputDir=..\bin\Installer
OutputBaseFilename=MermaidEditorSetup-{#MyAppVersion}
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Close running application before install/uninstall
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associatemmd"; Description: "Associate .mmd files with Mermaid Editor"; GroupDescription: "File associations:"
Name: "associatemermaid"; Description: "Associate .mermaid files with Mermaid Editor"; GroupDescription: "File associations:"
Name: "associatemd"; Description: "Associate .md files with Mermaid Editor"; GroupDescription: "File associations:"; Flags: unchecked

[Files]
; Include all files from the publish output directory
Source: "..\bin\Release\net10.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; File associations for .mmd files
Root: HKA; Subkey: "Software\Classes\.mmd"; ValueType: string; ValueName: ""; ValueData: "MermaidEditor.mmd"; Flags: uninsdeletevalue; Tasks: associatemmd
Root: HKA; Subkey: "Software\Classes\MermaidEditor.mmd"; ValueType: string; ValueName: ""; ValueData: "Mermaid Diagram File"; Flags: uninsdeletekey; Tasks: associatemmd
Root: HKA; Subkey: "Software\Classes\MermaidEditor.mmd\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associatemmd
Root: HKA; Subkey: "Software\Classes\MermaidEditor.mmd\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatemmd

; File associations for .mermaid files
Root: HKA; Subkey: "Software\Classes\.mermaid"; ValueType: string; ValueName: ""; ValueData: "MermaidEditor.mermaid"; Flags: uninsdeletevalue; Tasks: associatemermaid
Root: HKA; Subkey: "Software\Classes\MermaidEditor.mermaid"; ValueType: string; ValueName: ""; ValueData: "Mermaid Diagram File"; Flags: uninsdeletekey; Tasks: associatemermaid
Root: HKA; Subkey: "Software\Classes\MermaidEditor.mermaid\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associatemermaid
Root: HKA; Subkey: "Software\Classes\MermaidEditor.mermaid\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatemermaid

; File associations for .md files (Markdown)
Root: HKA; Subkey: "Software\Classes\.md"; ValueType: string; ValueName: ""; ValueData: "MermaidEditor.md"; Flags: uninsdeletevalue; Tasks: associatemd
Root: HKA; Subkey: "Software\Classes\MermaidEditor.md"; ValueType: string; ValueName: ""; ValueData: "Markdown File"; Flags: uninsdeletekey; Tasks: associatemd
Root: HKA; Subkey: "Software\Classes\MermaidEditor.md\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associatemd
Root: HKA; Subkey: "Software\Classes\MermaidEditor.md\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatemd

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check if a previous version is installed and return the uninstall string
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  // Check current user first (for per-user installs)
  if not RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString) then
    // Then check local machine (for all-users installs)
    RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

// Check if a previous version is installed
function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;

// Get the version of the previously installed application
function GetInstalledVersion(): String;
var
  sUnInstPath: String;
  sVersion: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sVersion := '';
  if not RegQueryStringValue(HKCU, sUnInstPath, 'DisplayVersion', sVersion) then
    RegQueryStringValue(HKLM, sUnInstPath, 'DisplayVersion', sVersion);
  Result := sVersion;
end;

// Uninstall the previous version silently
function UnInstallOldVersion(): Integer;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
  Result := 0;
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    if Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, iResultCode) then
      Result := 3
    else
      Result := 2;
  end else
    Result := 1;
end;

// Called during setup initialization
function InitializeSetup(): Boolean;
var
  sVersion: String;
  iResultCode: Integer;
begin
  Result := True;
  
  if IsUpgrade() then begin
    sVersion := GetInstalledVersion();
    if MsgBox('Version ' + sVersion + ' of {#MyAppName} is already installed. ' +
              'The previous version will be uninstalled before installing the new version.' + #13#10#13#10 +
              'Do you want to continue?', mbConfirmation, MB_YESNO) = IDNO then begin
      Result := False;
    end;
  end;
end;

// Called just before installation begins
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin
    if IsUpgrade() then begin
      UnInstallOldVersion();
    end;
  end;
end;
