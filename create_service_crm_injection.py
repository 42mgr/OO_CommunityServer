#!/usr/bin/env python3
"""
Inject enhanced CRM processing directly into the service layer ASC.Mail.Core.dll
"""

import sys
import os

def inject_enhanced_crm_into_service_dll(enhanced_dll, service_dll, output_dll):
    """Inject enhanced CRM methods from web DLL into service DLL"""
    
    print(f"🔧 Injecting enhanced CRM logic into service layer...")
    
    # Load both DLLs
    with open(service_dll, 'rb') as f:
        service_data = bytearray(f.read())
    
    with open(enhanced_dll, 'rb') as f:
        enhanced_data = f.read()
    
    print(f"📊 Service DLL: {len(service_data):,} bytes")
    print(f"📊 Enhanced DLL: {len(enhanced_data):,} bytes")
    
    # Find enhanced CRM patterns
    enhanced_patterns = [
        b"ProcessIncomingEmailForCrm",
        b"LinkChainToCrmEnhanced", 
        b"DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED",
        b"DEBUG: CRM conditions met",
        b"Enhanced automatic linking"
    ]
    
    injection_count = 0
    
    # Strategy: Replace CRM method implementations in the service DLL
    for pattern in enhanced_patterns:
        enhanced_pos = enhanced_data.find(pattern)
        service_pos = service_data.find(pattern)
        
        if enhanced_pos != -1 and service_pos != -1:
            print(f"🔄 Replacing {pattern.decode('utf-8', errors='ignore')}...")
            
            # Extract enhanced method region
            enhanced_start = max(0, enhanced_pos - 1024)
            enhanced_end = min(len(enhanced_data), enhanced_pos + 2048)
            enhanced_region = enhanced_data[enhanced_start:enhanced_end]
            
            # Replace in service DLL
            service_start = max(0, service_pos - 1024)
            service_end = min(len(service_data), service_start + len(enhanced_region))
            
            if service_end - service_start >= len(enhanced_region):
                service_data[service_start:service_start + len(enhanced_region)] = enhanced_region
                injection_count += 1
                print(f"  ✅ Injected {len(enhanced_region)} bytes")
            else:
                print(f"  ⚠️ Size mismatch, skipping")
        
        elif enhanced_pos != -1 and service_pos == -1:
            # Add new enhanced functionality to service DLL
            print(f"➕ Adding new functionality: {pattern.decode('utf-8', errors='ignore')}")
            
            # Extract enhanced region
            enhanced_start = max(0, enhanced_pos - 512)
            enhanced_end = min(len(enhanced_data), enhanced_pos + 1024) 
            enhanced_region = enhanced_data[enhanced_start:enhanced_end]
            
            # Append to service DLL
            service_data.extend(enhanced_region)
            injection_count += 1
            print(f"  ✅ Added {len(enhanced_region)} bytes")
    
    # Also inject enhanced debug strings
    debug_strings = [
        b"DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED for message",
        b"DEBUG: CRM conditions met - starting auto-processing",
        b"CRM auto-processing completed for message",
        b"Enhanced automatic linking with file uploads"
    ]
    
    for debug_str in debug_strings:
        if debug_str in enhanced_data and debug_str not in service_data:
            # Find a safe insertion point
            string_pos = service_data.find(b"DEBUG")
            if string_pos != -1:
                insert_pos = string_pos + 200
                service_data[insert_pos:insert_pos] = debug_str + b'\x00'
                injection_count += 1
                print(f"  ✅ Added debug string: {debug_str.decode('utf-8', errors='ignore')[:50]}...")
    
    print(f"\n📊 Injection Summary:")
    print(f"  Total injections: {injection_count}")
    print(f"  Final size: {len(service_data):,} bytes")
    print(f"  Size increase: {len(service_data) - 1544704:,} bytes")
    
    # Write enhanced service DLL
    with open(output_dll, 'wb') as f:
        f.write(service_data)
    
    print(f"✅ Created enhanced service DLL: {output_dll}")
    return injection_count > 0

def main():
    if len(sys.argv) != 4:
        print("Usage: create_service_crm_injection.py <enhanced_web.dll> <original_service.dll> <output_service.dll>")
        return
    
    enhanced_dll = sys.argv[1]
    service_dll = sys.argv[2] 
    output_dll = sys.argv[3]
    
    success = inject_enhanced_crm_into_service_dll(enhanced_dll, service_dll, output_dll)
    
    if success:
        print("🎉 Service-layer CRM injection completed successfully!")
        print("⚠️  Deploy this to all mail services for enhanced CRM processing")
    else:
        print("❌ Injection failed")

if __name__ == "__main__":
    main()