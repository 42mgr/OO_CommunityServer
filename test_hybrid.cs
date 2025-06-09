using System;
using System.Reflection;

class TestHybrid
{
    static void Main()
    {
        try
        {
            Console.WriteLine("Testing hybrid ASC.Mail.Core.dll assembly...");
            
            // Load the hybrid assembly
            var assembly = Assembly.LoadFrom("/root/claude/OO_CommunityServer/ASC.Mail.Core.enhanced.dll");
            
            Console.WriteLine($"✅ Assembly loaded: {assembly.FullName}");
            Console.WriteLine($"📊 Assembly size: {new System.IO.FileInfo("/root/claude/OO_CommunityServer/ASC.Mail.Core.enhanced.dll").Length:N0} bytes");
            
            // Check for enhanced CRM functionality
            var types = assembly.GetTypes();
            Console.WriteLine($"📊 Found {types.Length} types in assembly");
            
            // Look for CrmLinkEngine
            foreach (var type in types)
            {
                if (type.Name.Contains("CrmLinkEngine"))
                {
                    Console.WriteLine($"✅ Found CRM type: {type.FullName}");
                    
                    // Check for enhanced methods
                    var methods = type.GetMethods();
                    foreach (var method in methods)
                    {
                        if (method.Name.Contains("ProcessIncomingEmailForCrm") || 
                            method.Name.Contains("LinkChainToCrmEnhanced"))
                        {
                            Console.WriteLine($"  ✅ Enhanced method: {method.Name}");
                        }
                    }
                }
            }
            
            Console.WriteLine("🎉 Hybrid assembly test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}