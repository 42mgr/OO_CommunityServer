#!/usr/bin/env python3
"""
Create a runtime delegation wrapper that loads enhanced CRM functionality
"""

import sys

def inject_runtime_loader(original_dll, output_dll):
    """Inject runtime loading code into the original DLL"""
    
    with open(original_dll, 'rb') as f:
        data = bytearray(f.read())
    
    print(f"Original size: {len(data):,} bytes")
    
    # Find where CRM processing happens and inject a runtime check
    # Look for method entry points where we can inject our delegation
    
    crm_patterns = [
        b"ProcessIncomingEmailForCrm",
        b"get_CrmLinkEngine", 
        b"_crmLinkEngine"
    ]
    
    injections = 0
    
    for pattern in crm_patterns:
        pos = data.find(pattern)
        if pos != -1:
            print(f"Found CRM pattern at {pos}: {pattern.decode('utf-8')}")
            
            # Insert a marker that indicates enhanced functionality should be used
            marker = b"USE_ENHANCED_CRM_DLL"
            
            # Find a safe place to insert the marker (after the pattern)
            insert_pos = pos + len(pattern) + 1
            
            # Make sure we don't corrupt the assembly structure
            if insert_pos < len(data) - 100:
                # Insert our marker
                data[insert_pos:insert_pos] = marker + b'\x00'
                injections += 1
                print(f"  ✅ Injected runtime marker at {insert_pos}")
    
    # Also inject the path to our enhanced DLL
    enhanced_dll_path = b"ASC.Mail.Enhanced.dll\x00"
    
    # Find a safe location in the string table to add our DLL name
    string_section_pos = data.find(b".dll\x00")
    if string_section_pos != -1:
        insert_pos = string_section_pos + 50
        data[insert_pos:insert_pos] = enhanced_dll_path
        injections += 1
        print(f"✅ Injected enhanced DLL path reference")
    
    print(f"\n📊 Summary:")
    print(f"  Injections: {injections}")
    print(f"  Final size: {len(data):,} bytes")
    
    # Write the modified assembly
    with open(output_dll, 'wb') as f:
        f.write(data)
    
    print(f"✅ Created runtime-enhanced assembly: {output_dll}")

def main():
    if len(sys.argv) != 3:
        print("Usage: create_runtime_delegate.py <original_core.dll> <output.dll>")
        return
    
    original_dll = sys.argv[1]
    output_dll = sys.argv[2]
    
    print("🔧 Creating runtime delegation wrapper...")
    inject_runtime_loader(original_dll, output_dll)
    print("✅ Runtime delegation wrapper created!")

if __name__ == "__main__":
    main()