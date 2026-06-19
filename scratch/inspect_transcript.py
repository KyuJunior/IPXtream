import json
import os

def main():
    log_path = r"C:\Users\Dr. Yaser\.gemini\antigravity\brain\6708e901-4286-4cb8-8c62-a386d2d121a7\.system_generated\logs\transcript.jsonl"
    if not os.path.exists(log_path):
        print(f"Log path does not exist: {log_path}")
        return

    print("Reading transcript lines...")
    with open(log_path, "r", encoding="utf-8") as f:
        for line in f:
            try:
                data = json.loads(line)
                step_idx = data.get("step_index")
                source = data.get("source")
                type_ = data.get("type")
                
                if type_ == "USER_INPUT":
                    content = data.get("content")
                    print(f"\n[Step {step_idx}] USER: {content}")
                elif type_ == "PLANNER_RESPONSE":
                    content = data.get("content")
                    tool_calls = data.get("tool_calls", [])
                    print(f"[Step {step_idx}] AI Response (first 100 chars): {str(content)[:100]}")
                    if tool_calls:
                        for tc in tool_calls:
                            print(f"  Tool: {tc.get('name')} -> {str(tc.get('arguments'))[:150]}")
            except Exception as e:
                print(f"Error parsing line: {e}")

if __name__ == "__main__":
    main()
