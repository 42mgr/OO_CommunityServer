using System;
using System.Reflection;

class StartCrmService
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔍 Loading ASC.Mail assembly...");
            
            // Load the ASC.Mail assembly
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            
            Console.WriteLine("✅ Assembly loaded successfully");
            
            // Get the CrmEmailAutoLinkService type
            var serviceType = assembly.GetType("ASC.Mail.Core.Engine.CrmEmailAutoLinkService");
            if (serviceType == null)
            {
                Console.WriteLine("❌ CrmEmailAutoLinkService type not found");
                return;
            }
            
            Console.WriteLine("✅ Found CrmEmailAutoLinkService type");
            
            // Get the Start method
            var startMethod = serviceType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
            if (startMethod == null)
            {
                Console.WriteLine("❌ Start method not found");
                return;
            }
            
            Console.WriteLine("✅ Found Start method");
            
            // Call the Start method
            Console.WriteLine("🚀 Starting CRM Email Auto-Link Service...");
            startMethod.Invoke(null, null);
            
            Console.WriteLine("✅ CRM Email Auto-Link Service started successfully!");
            Console.WriteLine("Service will monitor for new emails every 30 seconds");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}