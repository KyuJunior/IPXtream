import json
import os

def main():
    log_path = r"C:\Users\Dr. Yaser\.gemini\antigravity\brain\6708e901-4286-4cb8-8c62-a386d2d121a7\.system_generated\logs\transcript.jsonl"
    if not os.path.exists(log_path):
        print("Transcript log path does not exist")
        return

    with open(log_path, "r", encoding="utf-8") as f:
        lines = f.readlines()
    
    print(f"Total steps in log: {len(lines)}")
    # We want to print detail for steps starting around 7915 to the end
    for line in lines[-50:]:
        try:
            data = json.loads(line)
            step_idx = data.get("step_index")
            source = data.get("source")
            type_ = data.get("type")
            content = data.get("content")
            
            print(f"\n--- STEP {step_idx} ({source} / {type_}) ---")
            if content:
                print(content[:500])
            tool_calls = data.get("tool_calls", [])
            if tool_calls:
                for tc in tool_calls:
                    print(f"Tool call: {tc.get('name')} with arguments: {tc.get('arguments')}")
        except Exception as e:
            pass

if __name__ == "__main__":
    main()
