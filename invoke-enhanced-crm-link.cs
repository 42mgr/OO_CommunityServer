using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;

/// <summary>
/// Direct Enhanced CRM Link Invoker
/// Loads OnlyOffice assemblies and directly calls LinkChainToCrmEnhanced
/// This demonstrates how to properly invoke the enhanced CRM logic
/// </summary>
class InvokeEnhancedCrmLink
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("🔗 Enhanced CRM Link Invoker");
            Console.WriteLine("============================");
            
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: invoke-enhanced-crm-link.exe <emailId1> [emailId2] [emailId3] ...");
                Console.WriteLine("Example: invoke-enhanced-crm-link.exe 5006 5007 5008 5009");
                Console.WriteLine("");
                Console.WriteLine("This will invoke LinkChainToCrmEnhanced for the specified email IDs");
                return;
            }
            
            // Configure assembly loading
            var binPath = "/var/www/onlyoffice/WebStudio/bin";
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                try
                {
                    var assemblyName = new AssemblyName(resolveArgs.Name).Name;
                    var assemblyPath = Path.Combine(binPath, assemblyName + ".dll");
                    if (File.Exists(assemblyPath))
                    {
                        Console.WriteLine($"  Loading assembly: {assemblyName}");
                        return Assembly.LoadFrom(assemblyPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Assembly resolve error for {resolveArgs.Name}: {ex.Message}");
                }
                return null;
            };
            
            Console.WriteLine("✅ Assembly resolver configured");
            
            // Load required assemblies
            Console.WriteLine("📚 Loading OnlyOffice assemblies...");
            var mailAssembly = Assembly.LoadFrom(Path.Combine(binPath, "ASC.Mail.dll"));
            var coreAssembly = Assembly.LoadFrom(Path.Combine(binPath, "ASC.Core.Common.dll"));
            
            Console.WriteLine("✅ Core assemblies loaded");
            
            // Process email IDs
            var emailIds = args.Select(arg => {
                if (int.TryParse(arg, out int id)) return id;
                throw new ArgumentException($"Invalid email ID: {arg}");
            }).ToArray();
            
            Console.WriteLine($"📧 Processing {emailIds.Length} email(s)...");
            
            var success = InvokeEnhancedLinking(mailAssembly, coreAssembly, emailIds);
            
            if (success)
            {
                Console.WriteLine("🎉 Enhanced CRM linking completed successfully!");
                Console.WriteLine("📋 Check the OnlyOffice CRM interface to verify emails appear in contact history");
            }
            else
            {
                Console.WriteLine("⚠️ Some emails may not have been processed successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    private static bool InvokeEnhancedLinking(Assembly mailAssembly, Assembly coreAssembly, int[] emailIds)
    {
        try
        {
            Console.WriteLine("🔧 Setting up OnlyOffice context...");
            
            // Get ILog type for constructor
            Type iLogType = null;
            try
            {
                var commonAssembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Common.dll");
                iLogType = commonAssembly.GetType("ASC.Common.Logging.ILog");
                if (iLogType == null)
                {
                    // Try log4net assembly
                    var log4netAssembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/log4net.dll");
                    iLogType = log4netAssembly.GetType("log4net.ILog");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not load ILog type: {ex.Message}");
            }
            
            // Get CrmLinkEngine type
            var crmLinkEngineType = mailAssembly.GetType("ASC.Mail.Core.Engine.CrmLinkEngine");
            if (crmLinkEngineType == null)
            {
                Console.WriteLine("❌ CrmLinkEngine type not found");
                return false;
            }
            
            // Get CrmContactData type
            var crmContactDataType = mailAssembly.GetType("ASC.Mail.Data.Contracts.CrmContactData");
            if (crmContactDataType == null)
            {
                Console.WriteLine("❌ CrmContactData type not found");
                return false;
            }
            
            Console.WriteLine("✅ Found required types");
            
            // Set up OnlyOffice context (tenant 0, admin user)
            var tenantId = 0;
            var userId = "00000000-0000-0000-0000-000000000000"; // Admin user
            
            // Try to set tenant context using CoreContext
            try
            {
                var coreContextType = coreAssembly.GetType("ASC.Core.CoreContext");
                var tenantManagerProperty = coreContextType.GetProperty("TenantManager");
                var tenantManager = tenantManagerProperty.GetValue(null);
                
                var setCurrentTenantMethod = tenantManager.GetType().GetMethod("SetCurrentTenant", new Type[] { typeof(int) });
                setCurrentTenantMethod.Invoke(tenantManager, new object[] { tenantId });
                
                Console.WriteLine($"✅ Set tenant context: {tenantId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not set tenant context: {ex.Message}");
            }
            
            // Create CrmLinkEngine instance - constructor is (int tenant, string user, ILog log = null)
            MethodBase crmLinkEngineConstructor = null;
            if (iLogType != null)
            {
                crmLinkEngineConstructor = crmLinkEngineType.GetConstructor(new Type[] { typeof(int), typeof(string), iLogType });
            }
            
            if (crmLinkEngineConstructor == null)
            {
                Console.WriteLine("❌ CrmLinkEngine constructor not found");
                var constructors = crmLinkEngineType.GetConstructors();
                Console.WriteLine($"Available constructors: {constructors.Length}");
                foreach (var ctor in constructors)
                {
                    var paramTypes = string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name));
                    Console.WriteLine($"  Constructor({paramTypes})");
                }
                return false;
            }
            
            // Invoke constructor with null log parameter
            var crmLinkEngine = ((ConstructorInfo)crmLinkEngineConstructor).Invoke(new object[] { tenantId, userId, null });
            
            Console.WriteLine("✅ Created CrmLinkEngine instance");
            
            // Find LinkChainToCrmEnhanced method
            var linkMethod = crmLinkEngineType.GetMethod("LinkChainToCrmEnhanced");
            if (linkMethod == null)
            {
                Console.WriteLine("❌ LinkChainToCrmEnhanced method not found");
                return false;
            }
            
            Console.WriteLine("✅ Found LinkChainToCrmEnhanced method");
            
            // Process each email
            var allSuccess = true;
            foreach (var emailId in emailIds)
            {
                Console.WriteLine($"🔗 Processing email {emailId}...");
                
                try
                {
                    // Create sample contact data for contact ID 4 (mgrafde@gmail.com)
                    var contactDataConstructor = crmContactDataType.GetConstructor(Type.EmptyTypes);
                    var contactData = contactDataConstructor.Invoke(null);
                    
                    // Set contact properties
                    var idProperty = crmContactDataType.GetProperty("Id");
                    var typeProperty = crmContactDataType.GetProperty("Type");
                    
                    idProperty.SetValue(contactData, 4);
                    
                    // Get EntityTypes enum
                    var entityTypesType = crmContactDataType.GetNestedType("EntityTypes");
                    if (entityTypesType != null)
                    {
                        var contactEnum = Enum.ToObject(entityTypesType, 1); // Contact = 1
                        typeProperty.SetValue(contactData, contactEnum);
                    }
                    
                    // Create list of contacts
                    var listType = typeof(List<>).MakeGenericType(crmContactDataType);
                    var contactsList = Activator.CreateInstance(listType);
                    var addMethod = listType.GetMethod("Add");
                    addMethod.Invoke(contactsList, new object[] { contactData });
                    
                    // Invoke LinkChainToCrmEnhanced
                    var parameters = linkMethod.GetParameters();
                    object[] invokeParams;
                    
                    if (parameters.Length == 3)
                    {
                        // LinkChainToCrmEnhanced(emailId, contactsList, httpScheme)
                        invokeParams = new object[] { emailId, contactsList, "http" };
                    }
                    else if (parameters.Length == 2)
                    {
                        // LinkChainToCrmEnhanced(emailId, contactsList)
                        invokeParams = new object[] { emailId, contactsList };
                    }
                    else
                    {
                        Console.WriteLine($"❌ Unexpected method signature for LinkChainToCrmEnhanced");
                        allSuccess = false;
                        continue;
                    }
                    
                    linkMethod.Invoke(crmLinkEngine, invokeParams);
                    Console.WriteLine($"  ✅ Successfully processed email {emailId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Error processing email {emailId}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"      Inner: {ex.InnerException.Message}");
                    }
                    allSuccess = false;
                }
            }
            
            return allSuccess;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in InvokeEnhancedLinking: {ex.Message}");
            return false;
        }
    }
}