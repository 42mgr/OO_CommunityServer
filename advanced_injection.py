#!/usr/bin/env python3
"""
Advanced injection of enhanced CRM functionality using binary patterns
"""

import sys
import os

def find_crm_engine_class(dll_data):
    """Find the CrmLinkEngine class location in the assembly"""
    patterns = [
        b"CrmLinkEngine",
        b"ProcessIncomingEmailForCrm", 
        b"get_CrmLinkEngine",
        b"_crmLinkEngine"
    ]
    
    positions = {}
    for pattern in patterns:
        pos = dll_data.find(pattern)
        if pos != -1:
            positions[pattern] = pos
            
    return positions

def extract_enhanced_crm_code(enhanced_dll):
    """Extract the enhanced CRM code regions from our DLL"""
    with open(enhanced_dll, 'rb') as f:
        data = f.read()
    
    # Find our enhanced methods
    enhanced_regions = {}
    
    # Look for unique strings from our enhanced code
    enhanced_markers = [
        b"LinkChainToCrmEnhanced",
        b"DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED",
        b"Enhanced automatic linking",
        b"CRM conditions met"
    ]
    
    for marker in enhanced_markers:
        pos = data.find(marker)
        if pos != -1:
            # Extract surrounding code region
            start = max(0, pos - 1024)
            end = min(len(data), pos + 2048)
            enhanced_regions[marker] = {
                'data': data[start:end],
                'offset': start,
                'marker_pos': pos - start
            }
            print(f"Found enhanced region for: {marker.decode('utf-8', errors='ignore')}")
    
    return enhanced_regions

def smart_inject(original_dll, enhanced_dll, output_dll):
    """Smart injection using pattern matching and code replacement"""
    
    print("🔍 Analyzing assemblies...")
    
    # Load both DLLs
    with open(original_dll, 'rb') as f:
        original_data = bytearray(f.read())
    
    # Extract enhanced regions
    enhanced_regions = extract_enhanced_crm_code(enhanced_dll)
    
    if not enhanced_regions:
        print("❌ No enhanced regions found!")
        return False
    
    print(f"📊 Original assembly: {len(original_data):,} bytes")
    print(f"📊 Found {len(enhanced_regions)} enhanced regions")
    
    # Find CRM engine locations in original
    original_crm_positions = find_crm_engine_class(original_data)
    print(f"📊 Found {len(original_crm_positions)} CRM patterns in original")
    
    injection_count = 0
    
    # Strategy 1: Append enhanced methods to end of assembly
    print("\n🔧 Strategy 1: Appending enhanced code...")
    
    for marker, region in enhanced_regions.items():
        enhanced_code = region['data']
        
        # Append to end of original assembly  
        append_pos = len(original_data)
        original_data.extend(enhanced_code)
        injection_count += 1
        
        print(f"  ✅ Appended {len(enhanced_code)} bytes at position {append_pos}")
    
    # Strategy 2: Try to replace existing CRM method stubs
    print("\n🔧 Strategy 2: Replacing method stubs...")
    
    # Look for simple method patterns that we can replace
    crm_method_patterns = [
        b"ProcessIncomingEmailForCrm",
        b"AutoLinkToCrm"
    ]
    
    for pattern in crm_method_patterns:
        original_pos = original_data.find(pattern)
        if original_pos != -1:
            print(f"  Found method pattern at {original_pos}: {pattern.decode('utf-8')}")
            
            # Find enhanced version
            for marker, region in enhanced_regions.items():
                if pattern in region['data']:
                    print(f"    Replacing with enhanced version...")
                    
                    # Replace a reasonable chunk around the method
                    replacement_size = min(512, len(region['data']))
                    end_pos = min(len(original_data), original_pos + replacement_size)
                    
                    original_data[original_pos:end_pos] = region['data'][:replacement_size]
                    injection_count += 1
                    print(f"    ✅ Replaced {replacement_size} bytes")
                    break
    
    print(f"\n📊 Final Summary:")
    print(f"  Total injections: {injection_count}")
    print(f"  Final size: {len(original_data):,} bytes")
    print(f"  Size increase: {len(original_data) - 1544704:,} bytes")
    
    # Write hybrid assembly
    with open(output_dll, 'wb') as f:
        f.write(original_data)
    
    print(f"✅ Created hybrid assembly: {output_dll}")
    return True

def main():
    if len(sys.argv) != 4:
        print("Usage: advanced_injection.py <original_core.dll> <enhanced_mail.dll> <output.dll>")
        return
    
    original_dll = sys.argv[1]
    enhanced_dll = sys.argv[2] 
    output_dll = sys.argv[3]
    
    success = smart_inject(original_dll, enhanced_dll, output_dll)
    
    if success:
        print("🎉 Advanced injection completed successfully!")
        print("⚠️  Note: This hybrid may need assembly validation")
    else:
        print("❌ Injection failed")

if __name__ == "__main__":
    main()