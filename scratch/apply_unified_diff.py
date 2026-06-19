import os
import re

def apply_hunks(file_path, hunks):
    if not os.path.exists(file_path):
        print(f"Error: Target file does not exist: {file_path}")
        return False
        
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
        
    # Normalize to LF
    original_crlf = '\r\n' in content
    content_lf = content.replace('\r\n', '\n')
    lines = content_lf.split('\n')
    
    # We apply hunks in reverse order (from last line to first line) so line numbers don't shift
    hunks.sort(key=lambda h: h['src_start'], reverse=True)
    
    for hunk in hunks:
        src_start = hunk['src_start']
        src_len = hunk['src_len']
        expected_lines = hunk['expected']
        replacement_lines = hunk['replacement']
        
        # In unified diff, line numbers are 1-indexed. Convert to 0-indexed.
        # Sometimes src_start is 0 for new files.
        start_idx = max(0, src_start - 1)
        
        # Verify if lines match expected lines
        actual_lines = lines[start_idx : start_idx + src_len]
        actual_stripped = [l.strip() for l in actual_lines]
        expected_stripped = [l.strip() for l in expected_lines]
        
        # If they don't match exactly, search in the vicinity
        match_found = False
        match_offset = 0
        
        # Search offset from -50 to +50 lines
        for offset in range(0, 100):
            for sign in [1, -1] if offset > 0 else [1]:
                check_idx = start_idx + offset * sign
                if check_idx < 0 or check_idx + src_len > len(lines):
                    continue
                check_lines = lines[check_idx : check_idx + src_len]
                check_stripped = [l.strip() for l in check_lines]
                if check_stripped == expected_stripped:
                    match_found = True
                    match_offset = offset * sign
                    break
            if match_found:
                break
                
        if not match_found:
            # Let's print a warning but continue if we can find a subset or try direct replacement
            print(f"Warning: Expected lines in {file_path} near line {src_start} did not match context.")
            print(f"Expected: {expected_stripped[:3]}")
            print(f"Actual: {actual_stripped[:3]}")
            return False
            
        final_start = start_idx + match_offset
        # Replace the lines
        lines[final_start : final_start + src_len] = replacement_lines
        
    new_content = '\n'.join(lines)
    if original_crlf:
        new_content = new_content.replace('\n', '\r\n')
        
    with open(file_path, 'w', encoding='utf-8', newline='') as f:
        f.write(new_content)
    print(f"Successfully applied patches to {file_path}")
    return True

def main():
    diff_path = "scratch/diff_utf8.txt"
    if not os.path.exists(diff_path):
        print(f"Error: Diff file not found: {diff_path}")
        return
        
    with open(diff_path, 'r', encoding='utf-8') as f:
        diff_lines = f.readlines()
        
    current_file = None
    hunks = []
    current_hunk = None
    
    file_patches = {}
    
    i = 0
    while i < len(diff_lines):
        line = diff_lines[i]
        
        if line.startswith("diff --git "):
            # Save previous file hunks
            if current_file and hunks:
                file_patches[current_file] = hunks
                hunks = []
            
            # Parse target filename
            match = re.match(r"diff --git a/(.*) b/(.*)", line.strip())
            if match:
                current_file = match.group(2)
            else:
                current_file = None
            i += 1
        elif line.startswith("@@ "):
            # Parse hunk header: @@ -src_start,src_len +dest_start,dest_len @@
            match = re.match(r"@@ -(\d+),?(\d*) \+(\d+),?(\d*) @@", line.strip())
            if match and current_file:
                src_start = int(match.group(1))
                src_len = int(match.group(2)) if match.group(2) else 1
                dest_start = int(match.group(3))
                dest_len = int(match.group(4)) if match.group(4) else 1
                
                current_hunk = {
                    'src_start': src_start,
                    'src_len': src_len,
                    'expected': [],
                    'replacement': []
                }
                hunks.append(current_hunk)
                
                # Parse hunk body
                i += 1
                while i < len(diff_lines) and not diff_lines[i].startswith("diff --git ") and not diff_lines[i].startswith("@@ "):
                    hline = diff_lines[i]
                    if hline.endswith('\n'):
                        hline = hline[:-1]
                    if hline.endswith('\r'):
                        hline = hline[:-1]
                        
                    if hline.startswith('-'):
                        current_hunk['expected'].append(hline[1:])
                    elif hline.startswith('+'):
                        current_hunk['replacement'].append(hline[1:])
                    elif hline.startswith(' '):
                        current_hunk['expected'].append(hline[1:])
                        current_hunk['replacement'].append(hline[1:])
                    elif hline == '':
                        # Empty line context
                        current_hunk['expected'].append('')
                        current_hunk['replacement'].append('')
                    else:
                        # Diff meta info
                        pass
                    i += 1
            else:
                i += 1
        else:
            i += 1
            
    # Save last file patches
    if current_file and hunks:
        file_patches[current_file] = hunks
        
    # Apply patches to text files
    for filepath, hunks_list in file_patches.items():
        # Ignore binary files
        if filepath.endswith('.exe') or filepath.endswith('.ico'):
            continue
            
        print(f"Applying hunks to {filepath}...")
        apply_hunks(filepath, hunks_list)

if __name__ == "__main__":
    main()
