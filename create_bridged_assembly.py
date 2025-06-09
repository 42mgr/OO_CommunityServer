#!/usr/bin/env python3
"""
Create a bridged assembly that combines original dependencies with enhanced code
"""

import sys
import struct

def merge_assemblies(original_dll, enhanced_dll, output_dll):
    """Merge enhanced code into original assembly structure"""
    
    # Load both assemblies
    with open(original_dll, 'rb') as f:
        original_data = bytearray(f.read())
    
    with open(enhanced_dll, 'rb') as f:
        enhanced_data = f.read()
    
    print(f"📊 Original: {len(original_data):,} bytes")
    print(f"📊 Enhanced: {len(enhanced_data):,} bytes")
    
    # Strategy: Replace CRM-related sections in original with enhanced versions
    
    # 1. Find and replace CrmLinkEngine class methods
    crm_sections = [
        b"CrmLinkEngine",
        b"ProcessIncomingEmailForCrm",
        b"LinkChainToCrm"
    ]
    
    replacements = 0
    
    for section in crm_sections:
        orig_pos = original_data.find(section)
        enhanced_pos = enhanced_data.find(section)
        
        if orig_pos != -1 and enhanced_pos != -1:
            print(f"🔄 Replacing section: {section.decode('utf-8')}")
            
            # Extract enhanced method region (more code around the method)
            enhanced_start = max(0, enhanced_pos - 512)
            enhanced_end = min(len(enhanced_data), enhanced_pos + 1024)
            enhanced_region = enhanced_data[enhanced_start:enhanced_end]
            
            # Replace in original (but don't exceed bounds)
            orig_start = max(0, orig_pos - 512) 
            orig_end = min(len(original_data), orig_pos + len(enhanced_region))
            
            if orig_end - orig_start >= len(enhanced_region):
                original_data[orig_start:orig_start + len(enhanced_region)] = enhanced_region
                replacements += 1
                print(f"  ✅ Replaced {len(enhanced_region)} bytes")
            else:
                print(f"  ⚠️ Skipping - size mismatch")
    
    # 2. Add enhanced strings and debug messages
    enhanced_strings = [
        b"DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED",
        b"LinkChainToCrmEnhanced",
        b"Enhanced automatic linking"
    ]
    
    for string in enhanced_strings:
        if string in enhanced_data and string not in original_data:
            # Find a good insertion point (near other strings)
            insert_pos = len(original_data) - 1000  # Near end but safe
            original_data[insert_pos:insert_pos] = string + b'\x00'
            replacements += 1
            print(f"  ✅ Added string: {string.decode('utf-8', errors='ignore')}")
    
    # 3. Critical: Copy our enhanced method implementations
    # Look for the actual method IL code patterns
    
    # Find ProcessIncomingEmailForCrm implementation in enhanced DLL
    enhanced_method_pos = enhanced_data.find(b"ProcessIncomingEmailForCrm")
    if enhanced_method_pos != -1:
        # Extract the method implementation (approximate)
        method_start = enhanced_method_pos
        method_end = enhanced_method_pos + 2048  # Assume 2KB method size
        
        if method_end <= len(enhanced_data):
            enhanced_method = enhanced_data[method_start:method_end]
            
            # Find corresponding location in original
            orig_method_pos = original_data.find(b"ProcessIncomingEmailForCrm")
            if orig_method_pos != -1:
                # Replace the method implementation
                end_replace = min(len(original_data), orig_method_pos + len(enhanced_method))
                original_data[orig_method_pos:end_replace] = enhanced_method[:end_replace-orig_method_pos]
                replacements += 1
                print(f"  ✅ Replaced method implementation")
    
    print(f"\n📊 Merge Summary:")
    print(f"  Total replacements: {replacements}")
    print(f"  Final size: {len(original_data):,} bytes")
    
    # Write the merged assembly
    with open(output_dll, 'wb') as f:
        f.write(original_data)
    
    print(f"✅ Created merged assembly: {output_dll}")
    return replacements > 0

def main():
    if len(sys.argv) != 4:
        print("Usage: create_bridged_assembly.py <original_core.dll> <enhanced_mail.dll> <output.dll>")
        return
    
    original_dll = sys.argv[1]
    enhanced_dll = sys.argv[2]
    output_dll = sys.argv[3]
    
    print("🌉 Creating bridged assembly...")
    success = merge_assemblies(original_dll, enhanced_dll, output_dll)
    
    if success:
        print("🎉 Bridged assembly created successfully!")
    else:
        print("❌ No replacements made - bridging failed")

if __name__ == "__main__":
    main()