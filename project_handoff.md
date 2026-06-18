# IPXtream Comprehensive Project Handoff Documentation

This document serves as the absolute, single source of truth for the **IPXtream** desktop IPTV client codebase, architectures, database schemas, hidden developer/admin codes, tokens, build/deploy pipelines, and engine mechanisms. It is designed to give any successor AI agent or human developer a complete, high-fidelity picture of the codebase.

---

## 1. Project Overview & Architectural Model
**IPXtream** is a premium, hardware-accelerated Windows Desktop IPTV client built using **.NET 8.0** and **WPF (Windows Presentation Foundation)**.

### Architectural Blueprint
*   **Design Pattern**: Model-View-ViewModel (MVVM) utilizing the **CommunityToolkit.Mvvm** source generators.
    *   **Views (Views/)**: XML-based markup (XAML) representing the presentation layers. UI logic (click handlers, animations) is in the code-behind files.
    *   **ViewModels (ViewModels/)**: State holders that bind to Views. They handle data flow, API calls, and window operations, and use properties generated with `[ObservableProperty]` and command bindings generated with `[RelayCommand]`.
    *   **Models (Models/)**: Simple Data Transfer Objects (DTOs) mapping JSON objects from the Xtream Codes API.
*   **Media Playback System**: Dual-engine integration (Flyleaf and VLC) to guarantee robust playback compatibility.
*   **Configuration Storage**: A single local file secured via Windows cryptography.
*   **Build Engine**: Custom Python orchestration scripts for build-increment-package-release automation.

---

## 2. Codebase Directory Map

```
c:\Myapps\ipxtream\
├── IPXtream_Installer.iss         # Inno Setup installer script for packaging release builds
├── swalalala.py                  # Auto-incrementing release automation script (compiles & releases to GitHub)
├── upload_release.py             # Script to upload compiled installers to GitHub releases manually
├── whats_new.json                # Local fallback/cache database for featured content on startup
├── IPXtream/                     # Core application directory
│   ├── App.xaml(.cs)             # App entry point, DI containers, and global color styles
│   ├── IPXtream.csproj           # C# project file detailing Nuget packages and file copies
│   ├── appicon.ico               # Windows system icon for taskbars and shortcuts
│   ├── dashboard_bg.png          # Ambient neon geometric background image
│   ├── FFMpeg/                   # Native FFmpeg DLLs copied to output directory (Flyleaf dependency)
│   ├── VCRuntime/                # C++ runtime DLLs bundled for self-contained execution
│   ├── Helpers/
│   │   ├── Converters.cs         # Converters for bindings (Visibility, multipliers, negation)
│   │   ├── CredentialStore.cs    # Encrypted settings file manager (DPAPI implementation)
│   │   ├── SectionToStyleConverter.cs # Swaps active styles on dashboard menu navigation
│   │   └── ThemeHelper.cs        # Theme manager applying dynamic styles in runtime
│   ├── Models/
│   │   ├── AppSettings.cs        # Preferences, saved accounts lists, and keys
│   │   ├── AuthResponse.cs       # Authentication status and provider attributes
│   │   ├── Category.cs           # Category structure mapping
│   │   ├── DownloadItem.cs       # Active state and thread controller for a download
│   │   ├── SeasonItem.cs         # Map for tv show seasons
│   │   ├── SeriesInfoResponse.cs # Maps nested episodes and seasons
│   │   ├── StreamItem.cs         # Represents Live channels, VOD Movies, or Series episodes
│   │   └── UserCredentials.cs    # Credentials container including host URL constructors
│   ├── Services/
│   │   ├── LogService.cs         # Diagnostic logging to app.log and player.log with token redaction
│   │   └── XtreamApiService.cs   # HTTP engine, local caching, and file download processor
│   ├── ViewModels/
│   │   ├── LoginViewModel.cs     # Input validation and account lists state
│   │   ├── DashboardViewModel.cs # Major dashboard loops, downloads registry, and settings updates
│   │   └── PlayerViewModel.cs    # Tracks play state, saves resume points, handles rendering views
│   └── Views/
│       ├── IconGeometries.xaml   # Path SVG geometries for high-dpi custom icons
│       ├── LoginWindow.xaml(.cs) # Custom Glassmorphism sign-in page
│       ├── DashboardWindow.xaml(.cs) # Grid categories explorer and media organizer
│       └── PlayerWindow.xaml(.cs)    # Full-screen dynamic player overlay
```

---

## 3. Database & Secured Settings Schema
IPXtream does not use a SQL database. Instead, it relies on a single serialized JSON state file secured on the Windows filesystem.

### Encryption System (`CredentialStore.cs`)
*   **Settings Path**: `C:\Users\<User>\AppData\Local\IPXtream\settings.dat`
*   **Encryption Technology**: Windows Data Protection API (**DPAPI**) via `System.Security.Cryptography.ProtectedData`.
*   **Security Scope**: `DataProtectionScope.CurrentUser` (cannot be decrypted on other user accounts or computers).
*   **Secret Entropy**: Initialized with a hardcoded byte sequence:
    `Encoding.UTF8.GetBytes("IPXtream_SecretEntropy_2024")`
*   **Startup Migrations**: Automatically looks for and migrates legacy config files (`credentials.dat` or `settings.json`) if detected at start, encrypting them into the unified `settings.dat`.

### JSON Configuration Schema (`AppSettings.cs`)
The decrypted contents represent a serialized JSON object of the `AppSettings` class:
```json
{
  "SavedAccounts": [
    {
      "Name": "Home Server",
      "ServerUrl": "http://example.com:8080",
      "Username": "myuser",
      "Password": "decrypted_in_memory_but_stored_encrypted"
    }
  ],
  "LastServerUrl": "http://example.com:8080",
  "LastUsername": "myuser",
  "LastPassword": "decrypted_in_memory_but_stored_encrypted",
  "AutoLogin": true,
  "ActiveThemeName": "Dark Purple",
  "SelectedPlayerEngine": "Flyleaf",
  "WindowLeft": 100.0,
  "WindowTop": 50.0,
  "WindowWidth": 1280.0,
  "WindowHeight": 720.0,
  "WindowMaximized": false,
  "DownloadSpeedLimitKbps": 0,
  "GithubToken": "ghp_PersonalAccessTokenStoredHereForAdminPushes"
}
```

---

## 4. Secret Admin & Developer Modes

IPXtream contains hidden developer controls built directly into the UI code-behind layers.

### Unlocking Admin Mode
*   **Trigger**: Click **5 times** in quick succession (within a `1500ms` window) on the **Version TextBlock** displayed in the Dashboard Window.
*   **Verification Prompt**: A tool window pops up requesting an admin password.
*   **Hardcoded Admin Password**: `ipxtreamadmin2026`
*   **Result**: Unlocks `IsAdminMode = true` on the `DashboardViewModel`. This toggles the visibility of the admin content controls.

### What's New ("Featured Library") Management
In Admin Mode, the user is presented with the **What's New** panel, allowing them to recommend and edit items in the featured library.
*   **Target File**: `whats_new.json`
*   **GitHub Endpoint**: `https://api.github.com/repos/KyuJunior/IPXtream/contents/whats_new.json`
*   **GitHub Personal Access Token (PAT)**:
    *   If missing from the encrypted `settings.dat`, the application pops up a prompt requesting a PAT (requires `repo` write scope).
    *   Once provided, it is securely written to `settings.dat` under `GithubToken`.
    *   Pushes are authenticated using the token via:
        `client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);`
    *   The API fetches the file's current Git SHA first, then puts the updated base64 JSON payload.

---

## 5. The Xtream Codes API Protocol

The app interfaces with IPTV servers using HTTP requests routed through `player_api.php`.

### API Requests
1.  **Authentication**:
    `GET {ServerURL}/player_api.php?username={Username}&password={Password}`
    *   **Invalid Credentials Response**: `{ "user_info": false }`
    *   **Success Response**: Returns a schema containing:
        *   `user_info`: contains `auth`, `status`, `exp_date`, `max_connections`, `active_cons`.
        *   `server_info`: contains host port, protocol, and `container_extension` capabilities (e.g. `mp4`, `mkv`, `ts`).
2.  **Category Retrieval**:
    *   Live TV: `&action=get_live_categories`
    *   VOD (Movies): `&action=get_vod_categories`
    *   Series (TV Shows): `&action=get_series_categories`
3.  **Stream Items Retrieval**:
    *   Live Channels: `&action=get_live_streams&category_id={id}`
    *   VOD Movies: `&action=get_vod_streams&category_id={id}`
    *   TV Series Metadata: `&action=get_series&category_id={id}`
    *   TV Series Seasons/Episodes: `&action=get_series_info&series_id={id}`

### Playback URL Assembly
Streams are directly resolved depending on category type:
*   **Live**: `{ServerURL}/live/{Username}/{Password}/{StreamId}.ts`
*   **VOD (Movies)**: `{ServerURL}/movie/{Username}/{Password}/{StreamId}.{Extension}`
*   **Series (Episodes)**: `{ServerURL}/series/{Username}/{Password}/{StreamId}.{Extension}`

---

## 6. Playback & Download Engines

### Playback Dual-Engine Architecture (`PlayerViewModel.cs`)
1.  **Flyleaf Player (Primary)**:
    *   Runs on high-performance native FFmpeg bindings.
    *   Extremely fast hardware-accelerated rendering and HLS stream buffer stability.
    *   Requires native FFmpeg DLL binaries inside the `FFMpeg/` subdirectory.
2.  **VLC Player (Fallback/LibVLCSharp)**:
    *   Automatically runs if SelectedPlayerEngine is changed, or as a compatibility fallback for audio tracks/container types Flyleaf cannot parse.
    *   Offers customized aspect ratio handling (e.g. `16:9`, `4:3`, `Fill`).

### Watch Progress Tracking
Both engines report playback progress intervals.
*   If a stream's elapsed playtime exceeds **10 seconds**, the app periodically writes metadata (StreamId, title, cover, and last position) to the local registry so that the item is displayed in the "Currently Watching" row on the Home page.

### Resumable & Throttled Download Engine (`XtreamApiService.cs`)
The Download system handles large video file acquisition with fault-tolerance:
*   **Temporary Files**: Writes streams to `{target_filename}.part` during transit.
*   **Connection Interruption Recovery**: When starting, it inspects if a `.part` file exists. If present, it initiates HTTP resumption by passing the file size in a request header:
    `Range: bytes={part_file_length}-`
*   **Renaming**: Renames the file to the final destination extension only when the stream is completed successfully.
*   **Bandwidth Throttling**:
    *   Throttling speed is governed by `limitKbps` configured in settings.
    *   Throttling Math: It tracks written bytes in a block loop. If the throughput exceeds the limit, it calculates the delay:
        `delayMs = targetMs - elapsedMs`
        and halts execution via `await Task.Delay(delayMs)`.
*   **Parallel Tasks**: Uses `Parallel.ForEachAsync` to speed up category indexing:
    *   Image posters caching: Up to 8 concurrent threads.
    *   Series info metadata: Up to 4 concurrent threads.

---

## 7. Build, Package, and Release Pipeline

The application features a fully automated Python script (`swalalala.py`) that handles versioning, compilation, building, and publishing.

### Build Actions Flow (`swalalala.py`)
1.  **Version Management**:
    *   Extracts the `<Version>X.Y.Z</Version>` tag in `IPXtream/IPXtream.csproj`.
    *   Increments the Patch version (e.g., `2.0.55` becomes `2.0.56`).
    *   Rewrites the project file with the incremented version.
2.  **Installer Script Updates**:
    *   Regex-updates `MyAppVersion` and output build directories inside the Inno Setup script `IPXtream_Installer.iss`.
3.  **Self-Contained Publish Compilation**:
    *   Executes .NET build:
        `dotnet publish IPXtream.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o IPXtream\bin\publish_v{new_version}`
4.  **Installer Packaging**:
    *   Invokes the Inno Setup compiler compiler:
        `C:\Program Files (x86)\Inno Setup 6\ISCC.exe IPXtream_Installer.iss`
    *   This generates the setup executable at `Output\IPXtream_Setup_v{new_version}.exe`.
5.  **Change logs / Release Notes**:
    *   Executes `git diff --name-status` to identify modified files.
    *   Generates categorized patch notes.
6.  **GitHub Release Upload**:
    *   Verifies GitHub CLI (`gh`) authentication status.
    *   Runs the `gh release create v{new_version}` command, uploads the compiled `IPXtream_Setup_v{new_version}.exe` setup installer, and publishes the changelog notes.

---

## 8. Diagnostic Logging & Credential Redaction (`LogService.cs`)
Diagnostics are split into two output files written to the app output path:
*   `app.log`: General runtime traces, DI, HTTP calls, and API responses.
*   `player.log`: FFmpeg and VLC decoder warnings and debug data.
*   **Privacy Guard**: The logger automatically parses and redacts user login details (passwords and usernames) from any Xtream connection URLs, replacing them with `[REDACTED_USER]` and `[REDACTED_PASS]` to prevent leaking credentials in diagnostics.
