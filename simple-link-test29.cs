using System;
using System.Reflection;
using System.Linq;

class SimpleLinkTest29
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔗 Simple approach to link Test 29 emails...");
            
            // Load the ASC.Mail assembly
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            var crmLinkEngineType = assembly.GetType("ASC.Mail.Core.Engine.CrmLinkEngine");
            
            // Show all constructors
            var constructors = crmLinkEngineType.GetConstructors();
            Console.WriteLine($"Found {constructors.Length} constructors:");
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                Console.WriteLine($"Constructor with {parameters.Length} parameters:");
                foreach (var param in parameters)
                {
                    Console.WriteLine($"  - {param.ParameterType.Name} {param.Name}");
                }
            }
            
            // Try the simplest constructor
            var constructor = constructors[0];
            var parameters2 = constructor.GetParameters();
            
            object[] args = new object[parameters2.Length];
            args[0] = 1; // tenant
            args[1] = "00000000-0000-0000-0000-000000000000"; // user
            if (parameters2.Length > 2)
            {
                args[2] = null; // log parameter
            }
            
            var crmEngine = constructor.Invoke(args);
            Console.WriteLine("✅ Created CrmLinkEngine instance");
            
            // Show available methods
            var methods = crmLinkEngineType.GetMethods().Where(m => m.Name.Contains("Link")).ToArray();
            Console.WriteLine($"Found {methods.Length} Link methods:");
            foreach (var method in methods.Take(3))
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