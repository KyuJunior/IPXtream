import subprocess
import sys

def main():
    try:
        # Run git diff
        result = subprocess.run(["git", "diff"], capture_output=True, text=True, encoding="utf-8")
        with open("scratch/diff_utf8.txt", "w", encoding="utf-8") as f:
            f.write(result.stdout)
        print("Successfully wrote diff to scratch/diff_utf8.txt")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()
