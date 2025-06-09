#!/usr/bin/env python3
"""
Directly patch the service layer ASC.Mail.Core.dll with enhanced CRM functionality
"""

import sys
import shutil

def patch_service_dll_with_enhanced_crm():
    """Replace CrmLinkEngine in service DLL with our enhanced version"""
    
    print("🔧 Patching service layer ASC.Mail.Core.dll with enhanced CRM functionality...")
    
    # Source: Enhanced CrmLinkEngine.cs from our web application
    source_file = "/root/claude/OO_CommunityServer/module/ASC.Mail/ASC.Mail/Core/Engine/CrmLinkEngine.cs"
    
    # Target: Copy enhanced source to container for compilation
    target_temp = "/tmp/enhanced_CrmLinkEngine.cs"
    
    try:
        # Read our enhanced CrmLinkEngine
        with open(source_file, 'r') as f:
            enhanced_code = f.read()
        
        print(f"📖 Read enhanced CrmLinkEngine.cs ({len(enhanced_code)} chars)")
        
        # Verify it contains our enhancements
        if "DEBUG: AddRelationshipEventForLinkedAccounts - No existing CRM links found" in enhanced_code:
            print("✅ Enhanced auto-linking code found in source")
        else:
            print("❌ Enhanced auto-linking code NOT found in source")
            return False
        
        # Write enhanced code to temp file for container compilation
        with open(target_temp, 'w') as f:
            f.write(enhanced_code)
        
        print(f"✅ Enhanced CrmLinkEngine.cs ready for deployment")
        return True
        
    except Exception as e:
        print(f"❌ Error patching service DLL: {e}")
        return False

def main():
    success = patch_service_dll_with_enhanced_crm()
    
    if success:
        print("\n🎯 Next steps to complete the fix:")
        print("1. Copy enhanced CrmLinkEngine.cs to container")
        print("2. Recompile ASC.Mail.Core.dll in container with enhanced code")
        print("3. Deploy to all mail services")
        print("4. Restart services to load enhanced functionality")
    else:
        print("❌ Patching failed")

if __name__ == "__main__":
    main()