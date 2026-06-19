import json
import os
import sys

# Ensure UTF-8 output
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

def main():
    log_path = r"C:\Users\Dr. Yaser\.gemini\antigravity\brain\6708e901-4286-4cb8-8c62-a386d2d121a7\.system_generated\logs\transcript.jsonl"
    if not os.path.exists(log_path):
        print("Transcript log path does not exist")
        return

    user_messages = []
    with open(log_path, "r", encoding="utf-8") as f:
        for line in f:
            try:
                data = json.loads(line)
                if data.get("type") == "USER_INPUT":
                    user_messages.append((data.get("step_index"), data.get("content")))
            except Exception as e:
                pass

    print(f"Total user messages: {len(user_messages)}")
    print("Last 30 user messages:")
    for step_idx, content in user_messages[-30:]:
        content_clean = content.encode('ascii', errors='replace').decode('ascii')
        print(f"  Step {step_idx}: {content_clean}")

if __name__ == "__main__":
    main()
