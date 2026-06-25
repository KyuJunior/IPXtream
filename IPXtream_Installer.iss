;  IPXtream v2.0.81 — Inno Setup 6 Installer Script
;
;  Step 1 — Publish the app first (run from IPXtream\ folder):
;    dotnet publish -c Release -r win-x64 --self-contained true ^
;      -p:PublishSingleFile=false ^
;      -o "bin\publish_v2.0.81"
;
;  Step 2 — Open this file in Inno Setup 6 and press F9.
;
;  Output: Output\IPXtream_Setup_v2.0.81.exe
; ============================================================

#define MyAppName      "IPXtream"
#define MyAppVersion   "2.0.81"
#define MyAppPublisher "Dr. Yaser"
#define MyAppURL       "https://github.com/KyuJunior/IPXtream"
#define MyAppExeName   "IPXtream.exe"
#define MyPublishDir   "IPXtream\bin\publish_v2.0.81"

[Setup]
AppId={{E7A2C3D4-F8B1-4E5A-9C6D-1234567890AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=IPXtream_Setup_v{#MyAppVersion}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; UI
WizardStyle=modern
WizardSizePercent=120
DisableWelcomePage=no

; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Architecture — 64-bit only
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; Upgrade — silently close app if running, allow in-place upgrade
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; \
  GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; ── Main executable ─────────────────────────────────────────────────────────
Source: "{#MyPublishDir}\{#MyAppExeName}";  DestDir: "{app}"; Flags: ignoreversion

; ── All runtime DLLs (self-contained .NET 8 runtime + app assemblies) ───────
Source: "{#MyPublishDir}\*.dll";            DestDir: "{app}"; Flags: ignoreversion

; ── Runtime config / manifest files ─────────────────────────────────────────
Source: "{#MyPublishDir}\*.json";           DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#MyPublishDir}\*.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; ── Bundled FFmpeg v7.1 native libraries ─────────────────────────────────────
Source: "{#MyPublishDir}\FFmpeg\*.dll";     DestDir: "{app}\FFmpeg"; Flags: ignoreversion
Source: "IPXtream\VCRuntime\*.dll";         DestDir: "{app}\FFmpeg"; Flags: ignoreversion

; ── Localization satellite assemblies ────────────────────────────────────────
Source: "{#MyPublishDir}\cs\*";   DestDir: "{app}\cs";   Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\de\*";   DestDir: "{app}\de";   Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\es\*";   DestDir: "{app}\es";   Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\fr\*";   DestDir: "{app}\fr";   Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\it\*";   DestDir: "{app}\it";   Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#MyPublishDir}\ja\*";   DestDir: "{app}\ja";   Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

[Icons]
; Start Menu
Name: "{group}\{#MyAppName}";              Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}";    Filename: "{uninstallexe}"

; Desktop (optional, unchecked by default)
Name: "{autodesktop}\{#MyAppName}";        Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Offer to launch after install
Filename: "{app}\{#MyAppExeName}"; \
  Description: "{cm:LaunchProgram,{#StringChange(MyAppName,'&','&&')}}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function VCIsInstalled(): Boolean;
var
  InstalledVal: Cardinal;
begin
  Result := False;
  // Check standard x64 VC++ 2015-2022 registry key
  if RegQueryDWordValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', InstalledVal) then begin
    Result := (InstalledVal = 1);
  end;
  if not Result then begin
    if RegQueryDWordValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', InstalledVal) then begin
      Result := (InstalledVal = 1);
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if CurPageID = wpReady then begin
    if not VCIsInstalled() then begin
      if MsgBox('IPXtream requires Microsoft Visual C++ Redistributable to play video streams.' + #13#10#13#10 +
                'Would you like the installer to download and install it now?', mbConfirmation, MB_YESNO) = idYes then begin
        try
          DownloadTemporaryFile('https://aka.ms/vs/17/release/vc_redist.x64.exe', 'vc_redist.x64.exe', '', nil);
          if Exec(ExpandConstant('{tmp}\vc_redist.x64.exe'), '/quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ErrorCode) then begin
            // Success
          end else begin
            MsgBox('Visual C++ Redistributable installation failed. Streams may fail to play.', mbError, MB_OK);
          end;
        except
          MsgBox('Failed to download Visual C++ Redistributable. Please ensure you are connected to the internet.', mbError, MB_OK);
        end;
      end;
    end;
  end;
end;

