import subprocess
import sys

def main():
    try:
        # Get all commits
        result = subprocess.run(["git", "log", "--oneline"], capture_output=True, text=True, encoding="utf-8")
        commits = result.stdout.strip().split("\n")
        
        for commit in commits:
            sha = commit.split()[0]
            # Get csproj contents at this commit
            show_res = subprocess.run(["git", "show", f"{sha}:IPXtream/IPXtream.csproj"], capture_output=True, text=True, encoding="utf-8")
            if "<Version>" in show_res.stdout:
                for line in show_res.stdout.split("\n"):
                    if "<Version>" in line:
                        print(f"Commit {commit}: {line.strip()}")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()
