import os
import re
import sys
import subprocess

# Ensure UTF-8 output for Windows console
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

def log(msg):
    print(f"[*] {msg}")

def error(msg):
    print(f"[ERROR] {msg}")
    sys.exit(1)

def main():
    csproj_path = os.path.join("IPXtream", "IPXtream.csproj")
    
    if not os.path.exists(csproj_path):
        error(f"Cannot find csproj file at: {csproj_path}")

    # 1. Read current version from csproj
    log("Reading current version from csproj...")
    with open(csproj_path, "r", encoding="utf-8") as f:
        csproj_content = f.read()

    ver_match = re.search(r"<Version>(\d+)\.(\d+)\.(\d+)</Version>", csproj_content)
    if not ver_match:
        error("Could not find <Version>X.Y.Z</Version> tag in csproj.")

    version = f"{ver_match.group(1)}.{ver_match.group(2)}.{ver_match.group(3)}"
    log(f"Current version is: {version}")

    # 2. Check if installer exists
    setup_file = f"Output\\IPXtream_Setup_v{version}.exe"
    if not os.path.exists(setup_file):
        error(f"Installer not found at: {setup_file}\nPlease compile it first using 'python swalalala.py' or build it manually.")

    # 3. Generate GitHub Release notes from the latest commit changes
    log("Generating Release Patch notes from latest commit...")
    notes_lines = []
    notes_lines.append(f"### [v{version}](https://github.com/KyuJunior/IPXtream/releases/tag/v{version})")
    notes_lines.append("\n#### 🚀 Features & Enhancements")

    has_diff_notes = False
    try:
        # Check diff between HEAD~1 and HEAD
        git_diff_stat = subprocess.run(
            ["git", "diff", "HEAD~1..HEAD", "--name-status"],
            capture_output=True, text=True, shell=True
        ).stdout
        
        # If no commit history or error, fallback to git status
        if not git_diff_stat:
            git_diff_stat = subprocess.run(
                ["git", "status", "--porcelain"],
                capture_output=True, text=True, shell=True
            ).stdout

        if git_diff_stat:
            seen_categories = set()
            category_notes = []
            for line in git_diff_stat.strip().split('\n'):
                parts = line.strip().split()
                if len(parts) < 2:
                    continue
                filename = parts[1]
                filename_base = os.path.basename(filename)
                
                if "Download" in filename_base or "XtreamApiService" in filename_base:
                    cat = "downloads"
                    if cat not in seen_categories:
                        category_notes.append("- **Downloads Engine**: Optimized segment downloads, pause/resume mechanisms, and speed limit throttling.")
                        seen_categories.add(cat)
                elif "DashboardWindow" in filename_base or "DashboardViewModel" in filename_base:
                    if "xaml" in filename:
                        cat = "ui"
                        if cat not in seen_categories:
                            category_notes.append("- **UI/UX Updates**: Integrated the new full-page Downloads Manager UI, and modernized seasons & episodes views.")
                            seen_categories.add(cat)
                    else:
                        cat = "nav"
                        if cat not in seen_categories:
                            category_notes.append("- **Series Navigation**: Resolved series cards playing directly as videos; they now load seasons/episodes like folders first, supported by a dedicated Back navigation control.")
                            seen_categories.add(cat)
                elif "Log" in filename_base or "Player" in filename_base:
                    cat = "logs"
                    if cat not in seen_categories:
                        category_notes.append("- **Integrated Logging System**: Added app.log and player.log with URL credential redaction for diagnostic troubleshooting.")
                        seen_categories.add(cat)

            if category_notes:
                notes_lines.append("Based on modified files, key updates in this patch include:")
                notes_lines.extend(category_notes)
                has_diff_notes = True
    except Exception:
        pass

    if not has_diff_notes:
        notes_lines.append("- General stability improvements, bug fixes, and performance optimizations.")

    notes_lines.append("\n#### 📦 Package details")
    notes_lines.append(f"- Installer name: `IPXtream_Setup_v{version}.exe`")

    notes_content = "\n".join(notes_lines)

    print("\n" + "="*80)
    print(f"GITHUB RELEASE TITLE: v{version}")
    print("="*80)
    print("GITHUB PATCH NOTES / CHANGELOG:")
    print("-" * 80)
    print(notes_content)
    print("-" * 80 + "\n")

    # 4. Check gh CLI
    log("Checking if GitHub CLI (gh) is installed...")
    gh_bin = "gh"
    try:
        gh_check = subprocess.run(["gh", "--version"], capture_output=True, text=True, shell=True)
        if gh_check.returncode != 0:
            raise FileNotFoundError
    except (FileNotFoundError, Exception):
        fallback = r"C:\Program Files\GitHub CLI\gh.exe"
        if os.path.exists(fallback):
            gh_bin = fallback
            log(f"Using GitHub CLI from fallback path: {gh_bin}")
        else:
            error("GitHub CLI (gh) is not installed. Please install it, or run 'winget install --id GitHub.cli', and authenticate using 'gh auth login'.")

    # 5. Check gh Auth Status
    log("Checking GitHub authentication status...")
    gh_auth = subprocess.run([gh_bin, "auth", "status"], capture_output=True, text=True, shell=True)
    if gh_auth.returncode != 0:
        error("You are not logged into GitHub CLI. Please run 'gh auth login' in your command prompt and try again.")

    # 6. Create GitHub Release
    log(f"Creating GitHub release v{version} and uploading {setup_file}...")
    notes_path = "temp_notes.md"
    with open(notes_path, "w", encoding="utf-8") as f:
        f.write(notes_content)

    try:
        gh_cmd = [
            gh_bin, "release", "create", f"v{version}",
            setup_file,
            "--title", f"v{version}",
            "--notes-file", notes_path
        ]
        res_gh = subprocess.run(gh_cmd, shell=True)
        if res_gh.returncode == 0:
            log("SUCCESS: GitHub release created and installer uploaded successfully!")
        else:
            error("GitHub release command failed. Please verify repository permissions or tag existence.")
    finally:
        if os.path.exists(notes_path):
            os.remove(notes_path)

if __name__ == "__main__":
    main()
