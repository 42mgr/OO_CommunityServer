using System;
using System.Reflection;
using System.Linq;

class TestServiceLogic
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔍 Testing CRM auto-link service logic...");
            
            // Load the ASC.Mail assembly
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            var serviceType = assembly.GetType("ASC.Mail.Core.Engine.CrmEmailAutoLinkService");
            
            // Test database connection and email extraction
            Console.WriteLine("Testing with email IDs 5002, 5003...");
            
            // Check if service has any public methods we can test
            var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            Console.WriteLine("Available public static methods:");
            foreach (var method in methods)
            {
                Console.WriteLine($"- {method.Name}");
            }
            
            // Try to get private/internal methods for diagnostics
            var allMethods = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
            Console.WriteLine("Available internal/private methods:");
            foreach (var method in allMethods.Take(5))
            {
                Console.WriteLine($"- {method.Name}");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}