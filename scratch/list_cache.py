import os
import json
import sys

sys.stdout.reconfigure(encoding='utf-8')
cache_dir = r"C:\Users\Dr. Yaser\AppData\Local\IPXtream\Cache"

for f in os.listdir(cache_dir):
    if not f.endswith('.json'):
        continue
    try:
        path = os.path.join(cache_dir, f)
        with open(path, "r", encoding="utf-8") as file:
            data = json.load(file)
            if isinstance(data, dict) and "info" in data and "episodes" in data:
                name = data["info"].get("name") if isinstance(data["info"], dict) else "Unknown"
                seasons = data.get("episodes", {})
                print(f"{f}: {name} -> {len(seasons)} seasons")
    except Exception as e:
        print(f"Error reading {f}: {e}")
