#define MyAppName "Spotlight Native"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Spotlight Native"
#define MyAppExeName "SpotlightNative.exe"

[Setup]
AppId={{D2AE4F9A-3D4D-4ED8-BB8D-2C2C5D4C8A7E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Spotlight Native
DefaultGroupName={#MyAppName}
OutputDir=..\output
OutputBaseFilename=Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startup"; Description: "Start Spotlight Native with Windows"; GroupDescription: "Additional options:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Spotlight Native"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\Spotlight Native"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SpotlightNative"; ValueData: """{app}\{#MyAppExeName}"" --autostart"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Spotlight Native"; Flags: nowait postinstall skipifsilent
