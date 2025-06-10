using System;
using System.Reflection;
using System.IO;

/// <summary>
/// Test Enhanced DLL Deployment
/// Verifies that the enhanced CRM functionality is properly loaded
/// </summary>
class TestEnhancedDeployment
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("🔍 Testing Enhanced DLL Deployment");
            Console.WriteLine("==================================");
            
            var binPath = "/var/www/onlyoffice/WebStudio/bin";
            
            // Configure assembly loading
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                try
                {
                    var assemblyName = new AssemblyName(resolveArgs.Name).Name;
                    var assemblyPath = Path.Combine(binPath, assemblyName + ".dll");
                    if (File.Exists(assemblyPath))
                    {
                        return Assembly.LoadFrom(assemblyPath);
                    }
                }
                catch { }
                return null;
            };
            
            Console.WriteLine("✅ Assembly resolver configured");
            
            // Test 1: Load ASC.Mail.dll
            Console.WriteLine("\n🧪 Test 1: Loading ASC.Mail.dll...");
            var mailAssembly = Assembly.LoadFrom(Path.Combine(binPath, "ASC.Mail.dll"));
            Console.WriteLine($"✅ Loaded ASC.Mail.dll - Size: {new FileInfo(Path.Combine(binPath, "ASC.Mail.dll")).Length} bytes");
            
            // Test 2: Check for CrmLinkEngine
            Console.WriteLine("\n🧪 Test 2: Looking for CrmLinkEngine...");
            var allTypes = mailAssembly.GetTypes();
            Console.WriteLine($"Total types in assembly: {allTypes.Length}");
            
            var crmTypes = Array.FindAll(allTypes, t => t.Name.Contains("Crm") || t.Name.Contains("CRM"));
            Console.WriteLine($"CRM-related types found: {crmTypes.Length}");
            
            foreach (var type in crmTypes)
            {
                Console.WriteLine($"  - {type.FullName}");
            }
            
            // Test 3: Look for LinkChainToCrmEnhanced method
            Console.WriteLine("\n🧪 Test 3: Looking for LinkChainToCrmEnhanced method...");
            foreach (var type in allTypes)
            {
                var methods = type.GetMethods();
                foreach (var method in methods)
                {
                    if (method.Name.Contains("LinkChain") && method.Name.Contains("Enhanced"))
                    {
                        Console.WriteLine($"✅ Found enhanced method: {type.FullName}.{method.Name}");
                        var parameters = method.GetParameters();
                        Console.WriteLine($"   Parameters: {parameters.Length}");
                        foreach (var param in parameters)
                        {
                            Console.WriteLine($"     - {param.ParameterType.Name} {param.Name}");
                        }
                    }
                }
            }
            
            // Test 4: Check for CrmEmailAutoLinkService
            Console.WriteLine("\n🧪 Test 4: Looking for CrmEmailAutoLinkService...");
            var autoLinkServiceType = Array.Find(allTypes, t => t.Name.Contains("CrmEmailAutoLinkService"));
            if (autoLinkServiceType != null)
            {
                Console.WriteLine($"✅ Found CrmEmailAutoLinkService: {autoLinkServiceType.FullName}");
                var methods = autoLinkServiceType.GetMethods();
                foreach (var method in methods)
                {
                    if (method.IsStatic && method.IsPublic)
                    {
                        Console.WriteLine($"   Static method: {method.Name}");
                    }
                }
            }
            else
            {
                Console.WriteLine("❌ CrmEmailAutoLinkService not found");
            }
            
            // Test 5: Check service layer DLL
            Console.WriteLine("\n🧪 Test 5: Checking service layer DLL...");
            var servicePath = "/var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Core.dll";
            if (File.Exists(servicePath))
            {
                var serviceInfo = new FileInfo(servicePath);
                Console.WriteLine($"✅ Service DLL found - Size: {serviceInfo.Length} bytes");
                Console.WriteLine($"   Last modified: {serviceInfo.LastWriteTime}");
                
                try
                {
                    var serviceAssembly = Assembly.LoadFrom(servicePath);
                    var serviceTypes = serviceAssembly.GetTypes();
                    var serviceCrmTypes = Array.FindAll(serviceTypes, t => t.Name.Contains("Crm"));
                    Console.WriteLine($"   CRM types in service: {serviceCrmTypes.Length}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️ Could not load service assembly: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("❌ Service DLL not found");
            }
            
            Console.WriteLine("\n🎯 Summary:");
            Console.WriteLine("- Enhanced DLLs have been deployed");
            Console.WriteLine("- Checking if enhanced CRM functionality is accessible");
            Console.WriteLine("- Ready to test actual CRM linking");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
}