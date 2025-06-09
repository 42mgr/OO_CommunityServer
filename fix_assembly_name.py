#!/usr/bin/env python3
"""
Fix assembly name in ASC.Mail.dll to make it work as ASC.Mail.Core.dll
"""

import sys
import struct

def fix_assembly_name(dll_path, output_path):
    """Replace ASC.Mail with ASC.Mail.Core in assembly metadata"""
    
    with open(dll_path, 'rb') as f:
        data = bytearray(f.read())
    
    # Replace all occurrences of "ASC.Mail" with "ASC.Mail.Core"
    # But be careful not to replace "ASC.Mail.Core" or longer strings
    
    original = b"ASC.Mail\x00"  # Null-terminated string
    replacement = b"ASC.Mail.Core\x00"
    
    # Find and replace assembly name references
    replacements = 0
    start = 0
    
    while True:
        pos = data.find(original, start)
        if pos == -1:
            break
            
        # Check if it's not already "ASC.Mail.Core"
        if pos + len(original) < len(data) and data[pos + len(original) - 1:pos + len(original) + 5] != b".Core":
            # Replace this occurrence
            data[pos:pos + len(original)] = replacement
            replacements += 1
            print(f"Replaced ASC.Mail with ASC.Mail.Core at position {pos}")
            start = pos + len(replacement)
        else:
            start = pos + 1
    
    # Also fix module name if present
    module_original = b"ASC.Mail.dll\x00"
    module_replacement = b"ASC.Mail.Core.dll\x00"
    
    start = 0
    while True:
        pos = data.find(module_original, start)
        if pos == -1:
            break
        data[pos:pos + len(module_original)] = module_replacement
        replacements += 1
        print(f"Replaced module name at position {pos}")
        start = pos + len(module_replacement)
    
    print(f"Made {replacements} replacements")
    
    # Write the fixed assembly
    with open(output_path, 'wb') as f:
        f.write(data)
    
    print(f"Created fixed assembly: {output_path}")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: fix_assembly_name.py <input.dll> <output.dll>")
        sys.exit(1)
    
    fix_assembly_name(sys.argv[1], sys.argv[2])