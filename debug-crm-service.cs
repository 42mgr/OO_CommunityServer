using System;
using System.Reflection;
using System.Threading;

class DebugCrmService
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔍 Debug CRM Email Auto-Link Service...");
            
            // Load the ASC.Mail assembly
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            var serviceType = assembly.GetType("ASC.Mail.Core.Engine.CrmEmailAutoLinkService");
            
            // Start the service
            var startMethod = serviceType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
            startMethod.Invoke(null, null);
            Console.WriteLine("✅ Service started");
            
            // Wait a bit then try to manually trigger the ProcessNewEmails method
            Thread.Sleep(5000);
            
            Console.WriteLine("🔄 Manually triggering ProcessNewEmails...");
            var processMethod = serviceType.GetMethod("ProcessNewEmails", BindingFlags.NonPublic | BindingFlags.Static);
            if (processMethod != null)
            {
                processMethod.Invoke(null, new object[] { null });
                Console.WriteLine("✅ ProcessNewEmails called");
            }
            else
            {
                Console.WriteLine("❌ ProcessNewEmails method not found");
            }
            
            Console.WriteLine("Service debug completed. Check what happened...");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                Console.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
            }
        }
    }
}