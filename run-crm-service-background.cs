using System;
using System.Reflection;
using System.Threading;

class RunCrmServiceBackground
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔍 Starting CRM Email Auto-Link Service in background...");
            
            // Load the ASC.Mail assembly
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            var serviceType = assembly.GetType("ASC.Mail.Core.Engine.CrmEmailAutoLinkService");
            
            // Get the Start method
            var startMethod = serviceType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
            
            // Start the service
            startMethod.Invoke(null, null);
            Console.WriteLine("✅ CRM Email Auto-Link Service started!");
            Console.WriteLine("Service will run in background. Check logs in /var/log/onlyoffice/");
            
            // Keep the process running
            Console.WriteLine("Press Ctrl+C to stop the service...");
            while (true)
            {
                Thread.Sleep(10000); // Sleep for 10 seconds
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Service is running...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}