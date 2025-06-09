#!/usr/bin/env python3
"""
Inject enhanced CRM functionality from ASC.Mail.dll into original ASC.Mail.Core.dll
"""

import sys
import os

def extract_method_region(dll_data, method_signature):
    """Extract a method and its surrounding IL code region"""
    method_bytes = method_signature.encode('utf-8')
    
    # Find method signature
    pos = dll_data.find(method_bytes)
    if pos == -1:
        return None, None
    
    # Extract a region around the method (this is simplified - real IL would need proper parsing)
    start = max(0, pos - 2048)  # 2KB before
    end = min(len(dll_data), pos + 4096)  # 4KB after
    
    return dll_data[start:end], start

def inject_enhanced_methods(original_dll, enhanced_dll, output_dll):
    """Inject enhanced CRM methods from enhanced_dll into original_dll"""
    
    print(f"Loading original ASC.Mail.Core.dll ({original_dll})...")
    with open(original_dll, 'rb') as f:
        original_data = bytearray(f.read())
    
    print(f"Loading enhanced ASC.Mail.dll ({enhanced_dll})...")  
    with open(enhanced_dll, 'rb') as f:
        enhanced_data = f.read()
    
    print(f"Original size: {len(original_data):,} bytes")
    print(f"Enhanced size: {len(enhanced_data):,} bytes")
    
    # Methods we want to inject
    enhanced_methods = [
        "ProcessIncomingEmailForCrm",
        "LinkChainToCrmEnhanced",
        "DEBUG: ProcessIncomingEmailForCrm",
        "DEBUG: CRM conditions met",
        "Enhanced automatic linking"
    ]
    
    injection_count = 0
    
    # Look for enhanced patterns and try to inject them
    for method in enhanced_methods:
        print(f"\nLooking for enhanced method/pattern: {method}")
        
        # Find in enhanced DLL
        enhanced_region, enhanced_pos = extract_method_region(enhanced_data, method)
        
        if enhanced_region:
            print(f"  Found in enhanced DLL at position {enhanced_pos}")
            
            # Look for similar pattern in original to replace
            method_base = method.split()[0] if " " in method else method
            original_pos = original_data.find(method_base.encode('utf-8'))
            
            if original_pos != -1:
                print(f"  Found target location in original at {original_pos}")
                
                # Simple replacement approach - replace a chunk around the method
                # This is crude but may work for method bodies
                chunk_size = min(len(enhanced_region), 1024)  # Limit chunk size
                
                # Make sure we don't go out of bounds
                if original_pos + chunk_size <= len(original_data):
                    # Replace the region
                    original_data[original_pos:original_pos + chunk_size] = enhanced_region[:chunk_size]
                    injection_count += 1
                    print(f"  ✅ Injected {chunk_size} bytes of enhanced code")
                else:
                    print(f"  ⚠️ Cannot inject - would exceed bounds")
            else:
                print(f"  ℹ️ No matching location found in original")
        else:
            print(f"  ℹ️ Not found in enhanced DLL")
    
    # Also inject our enhanced debug strings
    debug_strings = [
        b"DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED",
        b"DEBUG: CRM conditions met - starting auto-processing", 
        b"Enhanced automatic linking with file uploads",
        b"LinkChainToCrmEnhanced"
    ]
    
    for debug_str in debug_strings:
        if debug_str in enhanced_data and debug_str not in original_data:
            # Find a good place to inject the string (near other debug strings)
            debug_pos = original_data.find(b"DEBUG")
            if debug_pos != -1:
                # Insert the new debug string nearby
                insertion_point = debug_pos + 100
                original_data[insertion_point:insertion_point] = debug_str + b'\x00'
                injection_count += 1
                print(f"  ✅ Injected debug string: {debug_str.decode('utf-8', errors='ignore')}")
    
    print(f"\n📊 Summary:")
    print(f"  Total injections: {injection_count}")
    print(f"  Final size: {len(original_data):,} bytes")
    
    # Write the hybrid assembly
    with open(output_dll, 'wb') as f:
        f.write(original_data)
    
    print(f"✅ Created hybrid assembly: {output_dll}")

def main():
    if len(sys.argv) != 4:
        print("Usage: inject_enhanced_crm.py <original_core.dll> <enhanced_mail.dll> <output.dll>")
        print("  Creates hybrid DLL with enhanced CRM functionality injected into original")
        return
    
    original_dll = sys.argv[1]
    enhanced_dll = sys.argv[2]
    output_dll = sys.argv[3]
    
    if not os.path.exists(original_dll):
        print(f"ERROR: Original DLL not found: {original_dll}")
        return
        
    if not os.path.exists(enhanced_dll):
        print(f"ERROR: Enhanced DLL not found: {enhanced_dll}")
        return
    
    print("🔬 Injecting enhanced CRM functionality...")
    inject_enhanced_methods(original_dll, enhanced_dll, output_dll)
    print("🎉 Injection complete!")

if __name__ == "__main__":
    main()