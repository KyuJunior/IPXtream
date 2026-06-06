import os
import re
import sys
import subprocess

# Ensure UTF-8 output for Windows console/piping (handles emojis)
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

def log(msg):
    print(f"[*] {msg}")

def error(msg):
    print(f"[ERROR] {msg}")
    sys.exit(1)

def main():
    csproj_path = os.path.join("IPXtream", "IPXtream.csproj")
    iss_path = "IPXtream_Installer.iss"

    if not os.path.exists(csproj_path):
        error(f"Cannot find csproj file at: {csproj_path}")
    if not os.path.exists(iss_path):
        error(f"Cannot find Inno Setup file at: {iss_path}")

    # 1. Read current version from csproj
    log("Reading current version from csproj...")
    with open(csproj_path, "r", encoding="utf-8") as f:
        csproj_content = f.read()

    ver_match = re.search(r"<Version>(\d+)\.(\d+)\.(\d+)</Version>", csproj_content)
    if not ver_match:
        error("Could not find <Version>X.Y.Z</Version> tag in csproj.")

    major, minor, patch = map(int, ver_match.groups())
    old_version = f"{major}.{minor}.{patch}"
    new_patch = patch + 1
    new_version = f"{major}.{minor}.{new_patch}"

    log(f"Current version: {old_version}")
    log(f"New version will be: {new_version}")

    # 2. Update csproj with new version
    log("Updating version in csproj...")
    new_csproj_content = re.sub(
        r"<Version>\d+\.\d+\.\d+</Version>",
        f"<Version>{new_version}</Version>",
        csproj_content
    )
    with open(csproj_path, "w", encoding="utf-8") as f:
        f.write(new_csproj_content)
    log("csproj updated successfully.")

    # 3. Update ISS installer script
    log("Updating version in Inno Setup installer script...")
    with open(iss_path, "r", encoding="utf-8") as f:
        iss_content = f.read()

    # Update define version
    iss_content = re.sub(
        r'#define MyAppVersion\s+"[^"]+"',
        lambda m: f'#define MyAppVersion   "{new_version}"',
        iss_content
    )

    # Update publish directory define
    iss_content = re.sub(
        r'#define MyPublishDir\s+"[^"]+"',
        lambda m: f'#define MyPublishDir   "IPXtream\\bin\\publish_v{new_version}"',
        iss_content
    )

    # Update header version comment references
    iss_content = re.sub(
        r';\s+IPXtream v\d+\.\d+\.\d+ — Inno Setup 6 Installer Script',
        lambda m: f';  IPXtream v{new_version} — Inno Setup 6 Installer Script',
        iss_content
    )
    iss_content = re.sub(
        r'-o "bin\\publish_v\d+\.\d+\.\d+"',
        lambda m: f'-o "bin\\publish_v{new_version}"',
        iss_content
    )
    iss_content = re.sub(
        r';\s+Output: Output\\IPXtream_Setup_v\d+\.\d+\.\d+\.exe',
        lambda m: f';  Output: Output\\IPXtream_Setup_v{new_version}.exe',
        iss_content
    )

    with open(iss_path, "w", encoding="utf-8") as f:
        f.write(iss_content)
    log("Inno Setup script updated successfully.")

    # 4. Compile / Publish the .NET application
    publish_dir = f"bin\\publish_v{new_version}"
    log(f"Publishing dotnet project to {publish_dir}...")
    publish_cmd = [
        "dotnet", "publish",
        csproj_path,
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=false",
        "-o", os.path.join("IPXtream", publish_dir)
    ]
    
    # Run publishing
    res = subprocess.run(publish_cmd, shell=True)
    if res.returncode != 0:
        error("dotnet publish failed. Aborting installer compilation.")
    log("Publishing completed successfully.")

    # 5. Compile the Installer using ISCC.exe
    iscc_path = r"C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if not os.path.exists(iscc_path):
        error(f"Inno Setup Compiler not found at: {iscc_path}")

    log("Compiling Inno Setup installer package...")
    iscc_cmd = [iscc_path, iss_path]
    res_iscc = subprocess.run(iscc_cmd, shell=True)
    if res_iscc.returncode != 0:
        error("Inno Setup installer compilation failed.")
    
    setup_file = f"Output\\IPXtream_Setup_v{new_version}.exe"
    if os.path.exists(setup_file):
        log(f"SUCCESS: Installer built at {setup_file}")
    else:
        error(f"Installer was not found at expected location: {setup_file}")

    # 6. Generate GitHub Release details
    log("Generating Release Patch notes...")
    print("\n" + "="*80)
    print(f"GITHUB RELEASE TITLE: v{new_version}")
    print("="*80)
    print("GITHUB PATCH NOTES / CHANGELOG:")
    print("-" * 80)
    print(f"### [v{new_version}](https://github.com/KyuJunior/IPXtream/releases/tag/v{new_version})")
    print("\n#### 🚀 Features & Enhancements")
    
    # Try getting git status/diff info to auto-generate notes
    try:
        git_diff_stat = subprocess.run(["git", "diff", "--name-status"], capture_output=True, text=True, shell=True).stdout
        if git_diff_stat:
            print("Based on modified files, key updates in this patch include:")
            modified_files = [line.strip().split() for line in git_diff_stat.strip().split('\n') if line]
            for status, filename in modified_files:
                filename_base = os.path.basename(filename)
                if "Download" in filename_base or "XtreamApiService" in filename_base:
                    print(f"- **Downloads Engine**: Optimized segment downloads, pause/resume mechanisms, and speed limit throttling.")
                elif "DashboardWindow" in filename_base or "DashboardViewModel" in filename_base:
                    if "xaml" in filename:
                        print(f"- **UI/UX Updates**: Integrated the new full-page Downloads Manager UI, and modernized seasons & episodes views.")
                    else:
                        print(f"- **Series Navigation**: Resolved series cards playing directly as videos; they now load seasons/episodes like folders first, supported by a dedicated Back navigation control.")
        else:
            print("- General stability improvements and bug fixes.")
    except Exception:
        print("- General maintenance, performance improvements, and interface updates.")
        
    print("\n#### 📦 Package details")
    print(f"- Installer name: `IPXtream_Setup_v{new_version}.exe`")
    print("-" * 80 + "\n")

if __name__ == "__main__":
    main()
