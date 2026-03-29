[Setup]
AppName=IPXtream
AppVersion=1.4
AppPublisher=Dr. Yaser
DefaultDirName={pf}\IPXtream
DefaultGroupName=IPXtream
OutputDir=Output
OutputBaseFilename=IPXtream_Installer_v1.4
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=IPXtream\ipxtream_logo.ico
UninstallDisplayIcon={app}\IPXtream.exe

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; IMPORTANT: Make sure you publish the app first! 
; Run: dotnet publish -c Release -r win-x64 --self-contained true
Source: "IPXtream\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\IPXtream"; Filename: "{app}\IPXtream.exe"
Name: "{group}\{cm:UninstallProgram,IPXtream}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\IPXtream"; Filename: "{app}\IPXtream.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\IPXtream.exe"; Description: "{cm:LaunchProgram,IPXtream}"; Flags: nowait postinstall skipifsilent
