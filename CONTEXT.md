# IPXtream Active Context & History

This document preserves the active context, goals, accomplished tasks, and operational workflows of the **IPXtream** desktop client development. It serves as a handoff and reference guide for any developer or AI assistant working on the project.

---

## 1. The Core Idea & Concept
**IPXtream** is a premium, hardware-accelerated Windows Desktop IPTV client built using **.NET 8.0** and **WPF (Windows Presentation Foundation)**.
* **Goal**: Provide a sleek, modern, media-center-style desktop client for IPTV services that support the Xtream Codes API.
* **Aesthetic**: Premium dark themes, glassmorphism, responsive grid lists, smooth visual transitions, and customized media player controls (using Flyleaf and VLC).
* **Release Pipeline**: Packages a self-contained `.exe` installer using Inno Setup 6, enabling seamless, zero-dependency deployment and updating.

---

## 2. Active Development Goals (Current State)
We are currently in a polish and maintenance phase. Recent work centered on sidebar navigation improvements and release automation.
* **Mandatory Versioning Rule**: After every change/feature implementation, the version must be incremented by `+0.0.1` (e.g., from `2.0.90` to `2.0.91`) before building, publishing, or creating a release.
* **Done**: 
  - Restyled Dashboard navigation by moving Home & Downloads into the sidebar.
  - Removed top action bar navigation buttons to clean up clutter.
  - Linked active navigation style highlighting using a custom converter.
  - Implemented the Obsidian Cinema theme and dynamic Ambilight poster glow backdrop.
  - Created project-specific custom skills under `.agents/skills/`.
  - Version-bumped to `2.0.97` and successfully published & pushed the GitHub release.
* **Next Steps**:
  - Await next feature requests or bug reports.
  - Address any issues related to media playback, settings, or multi-account overlays.

---

## 3. Operational Workflows & Commands

### A. Automatic Bump, Build & Release (The "Swalalala" Workflow)
The user defined "swalalala" as a complete workflow: bump version by `+0.0.1`, publish in release mode, compile the setup installer, generate patch notes, push to GitHub, and create a release uploading the installer.
This is automated using the `swalalala.py` script:
```powershell
# Clears environment GITHUB_TOKEN/GH_TOKEN to ensure gh CLI keyring auth is used, then runs the workflow:
$env:GITHUB_TOKEN = $null; $env:GH_TOKEN = $null; python swalalala.py
```
After running, commit and push the updated version files:
```powershell
git add IPXtream/IPXtream.csproj IPXtream_Installer.iss CONTEXT.md
git commit -m "Bump version to X.Y.Z after successful release"
$env:GITHUB_TOKEN = $null; $env:GH_TOKEN = $null; git push
```

### B. Release Integrity & In-App Update Mechanism
For the in-app update check to function correctly, the following criteria must be met:
1. **Installer Asset Naming**: The compiled installer must be named `IPXtream_Setup_v{Version}.exe` (matching the version defined in `IPXtream.csproj`).
2. **Asset Upload**: The setup `.exe` must be uploaded as a release asset in the GitHub Release using the GitHub CLI (`gh release create v{Version} Output/IPXtream_Setup_v{Version}.exe`).
3. **Primary Checker**: The primary update checker parses the release assets list looking for any asset ending with `.exe`.
4. **Fallback Checker**: The fallback update mechanism builds a direct download URL matching `https://github.com/KyuJunior/IPXtream/releases/download/{tagName}/IPXtream_Setup_{tagName}.exe`, which requires the asset filename to match the release tag name exactly (e.g., tag `v2.0.91` downloads `IPXtream_Setup_v2.0.91.exe`).
5. **Git Push**: Ensure all modified tracking files (`IPXtream/IPXtream.csproj`, `IPXtream_Installer.iss`, `CONTEXT.md`) are committed and pushed to GitHub main branch.

### C. Standard Local Build & Publish
* **Dotnet Build**:
  ```powershell
  dotnet build IPXtream\IPXtream.csproj
  ```
* **Dotnet Publish (Manual)**:
  ```powershell
  dotnet publish IPXtream\IPXtream.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o IPXtream\bin\publish_v2.0.90
  ```
* **Inno Setup Compilation (Manual)**:
  ```powershell
  & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" IPXtream_Installer.iss
  ```

---

## 4. Key Code Locations & Mechanisms

### A. Navigation & Styling
* **Sidebar UI**: Main navigation container inside [DashboardWindow.xaml](file:///c:/Myapps/ipxtream/IPXtream/Views/DashboardWindow.xaml).
* **Navigation Active Highlighter**: [SectionToStyleConverter.cs](file:///c:/Myapps/ipxtream/IPXtream/Helpers/SectionToStyleConverter.cs) converts the selected `MediaSection` enum value into standard or active navigation button resource styles.
* **Default Page on Launch**: `_activeSection` in [DashboardViewModel.cs](file:///c:/Myapps/ipxtream/IPXtream/ViewModels/DashboardViewModel.cs) defaults to `MediaSection.Home` to load the landing page.

### B. Secured Credentials & Settings
* **Storage Path**: `C:\Users\<User>\AppData\Local\IPXtream\settings.dat`
* **Encryption**: DPAPI (Windows Cryptography) via [CredentialStore.cs](file:///c:/Myapps/ipxtream/IPXtream/Helpers/CredentialStore.cs).
* **Theme Handling**: Dynamic resource injection managed in [ThemeHelper.cs](file:///c:/Myapps/ipxtream/IPXtream/Helpers/ThemeHelper.cs).

---

## 5. Development History (Recent Releases)
* **v2.0.97**:
  - Implemented "Obsidian Cinema" theme and dynamic "Ambilight" poster glow backdrop.
  - Fixed cover cache bindings with CachedImageConverter.
  - Created `.agents/skills/` directory with wpf-mvvm-and-styling, iptv-performance-and-caching, media-player-lifecycle, and release-integrity rules.
  - Automatically compiled, packaged, and released.
* **v2.0.90**:
  - Automatically compiled, packaged, and released.
  - Staged and pushed version files to the main branch.
* **v2.0.89**:
  - Initial version bump testing.
* **v2.0.88**:
  - Redesigned the navigation bar: moved Home and Downloads to the sidebar panel with matching icons.
  - Cleaned up redundant top menu navigation buttons.
  - Made the `SectionToStyleConverter` converter case-insensitive for reliable style swaps.
