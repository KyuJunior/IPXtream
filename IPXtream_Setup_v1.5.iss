; ============================================================
;  IPXtream v1.5.0 — Inno Setup 6 Script
;  Builds a self-contained Windows installer for IPXtream.
;
;  Prerequisites:
;    1. Inno Setup 6 (https://jrsoftware.org/isinfo.php)
;    2. Run the publish command first:
;       dotnet publish -c Release -r win-x64 --self-contained true ^
;         -p:PublishSingleFile=false -o "bin\publish_v1.5"
;    3. Save this file to:  c:\Myapps\ipxtream\IPXtream_Setup_v1.5.iss
;    4. Open in Inno Setup and press F9 to compile.
; ============================================================

#define MyAppName      "IPXtream"
#define MyAppVersion   "1.5.0"
#define MyAppPublisher "IPXtream"
#define MyAppURL       "https://github.com/your-org/ipxtream"
#define MyAppExeName   "IPXtream.exe"
#define MyPublishDir   "IPXtream\bin\publish_v1.5"

[Setup]
AppId={{E7A2C3D4-F8B1-4E5A-9C6D-1234567890AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\Installer
OutputBaseFilename=IPXtream_Setup_v{#MyAppVersion}
SetupIconFile=appicon_32.ico
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
WizardSizePercent=120
DisableWelcomePage=no
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\*.dll";           DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\*.json";          DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#MyPublishDir}\FFmpeg\*.dll";    DestDir: "{app}\FFmpeg"; Flags: ignoreversion
Source: "{#MyPublishDir}\cs\*";  DestDir: "{app}\cs";  Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\de\*";  DestDir: "{app}\de";  Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\es\*";  DestDir: "{app}\es";  Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\fr\*";  DestDir: "{app}\fr";  Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\it\*";  DestDir: "{app}\it";  Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\ja\*";  DestDir: "{app}\ja";  Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}";           FileName: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; FileName: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";     FileName: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
