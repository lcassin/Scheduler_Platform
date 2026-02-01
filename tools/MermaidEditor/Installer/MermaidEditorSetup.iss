; Inno Setup Script for Mermaid Editor
; This script creates a Windows installer for the Mermaid Editor application

#define MyAppName "Mermaid Editor"
#define MyAppVersion "1.2"
#define MyAppPublisher "Scheduler Platform"
#define MyAppURL "https://github.com/lcassin/Scheduler_Platform"
#define MyAppExeName "MermaidEditor.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{8F3E4A2B-1C5D-4E6F-9A8B-7C2D3E4F5A6B}
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associatemmd"; Description: "Associate .mmd files with Mermaid Editor"; GroupDescription: "File associations:"
Name: "associatemermaid"; Description: "Associate .mermaid files with Mermaid Editor"; GroupDescription: "File associations:"
Name: "associatemd"; Description: "Associate .md files with Mermaid Editor"; GroupDescription: "File associations:"; Flags: unchecked

[Files]
; Include all files from the publish output directory
Source: "..\bin\Release\net10.0-windows\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
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
