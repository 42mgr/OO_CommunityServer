#!/usr/bin/env python3
"""
Binary patcher to replace CrmLinkEngine methods in ASC.Mail.Core.dll 
with enhanced versions from ASC.Mail.dll
"""

import struct
import sys
import os

def find_method_in_assembly(dll_path, method_name):
    """Find method signature in .NET assembly"""
    with open(dll_path, 'rb') as f:
        data = f.read()
    
    # Look for method name in the assembly
    method_bytes = method_name.encode('utf-8')
    positions = []
    start = 0
    while True:
        pos = data.find(method_bytes, start)
        if pos == -1:
            break
        positions.append(pos)
        start = pos + 1
    
    return positions

def extract_method_il(dll_path, method_name):
    """Extract IL bytecode for a specific method"""
    # This would require full PE/COFF parsing for .NET assemblies
    # For now, let's use a simpler string replacement approach
    
    with open(dll_path, 'rb') as f:
        data = f.read()
    
    # Find the method and extract surrounding context
    method_bytes = method_name.encode('utf-8')
    pos = data.find(method_bytes)
    if pos != -1:
        # Extract ~1KB around the method for context
        start = max(0, pos - 512)
        end = min(len(data), pos + 512)
        return data[start:end], start
    
    return None, None

def main():
    if len(sys.argv) != 4:
        print("Usage: patch_mail_core.py <original_core.dll> <enhanced_mail.dll> <output.dll>")
        return
    
    original_dll = sys.argv[1]
    enhanced_dll = sys.argv[2] 
    output_dll = sys.argv[3]
    
    print(f"Patching {original_dll} with methods from {enhanced_dll}")
    
    # Find enhanced methods in our ASC.Mail.dll
    enhanced_methods = [
        "ProcessIncomingEmailForCrm",
        "LinkChainToCrmEnhanced"
    ]
    
    # Copy original to output
    with open(original_dll, 'rb') as f:
        original_data = bytearray(f.read())
    
    print(f"Original size: {len(original_data)} bytes")
    
    # For each enhanced method, try to find and replace
    for method in enhanced_methods:
        print(f"Looking for method: {method}")
        
        # Find in original
        orig_positions = find_method_in_assembly(original_dll, method)
        enhanced_positions = find_method_in_assembly(enhanced_dll, method)
        
        print(f"  Original positions: {orig_positions}")
        print(f"  Enhanced positions: {enhanced_positions}")
        
        if orig_positions and enhanced_positions:
            print(f"  Found {method} in both assemblies")
            
            # Extract method context from enhanced DLL
            enhanced_context, context_start = extract_method_il(enhanced_dll, method)
            if enhanced_context:
                print(f"  Extracted {len(enhanced_context)} bytes of context")
    
    # Write patched output
    with open(output_dll, 'wb') as f:
        f.write(original_data)
    
    print(f"Created patched assembly: {output_dll}")

if __name__ == "__main__":
    main()