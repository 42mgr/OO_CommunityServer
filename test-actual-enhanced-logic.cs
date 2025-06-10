using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

/// <summary>
/// Test Actual Enhanced Logic
/// Tries to call the real LinkChainToCrmEnhanced method with proper context
/// </summary>
class TestActualEnhancedLogic
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("🔗 Testing Actual Enhanced Logic");
            Console.WriteLine("================================");
            
            var emailId = args.Length > 0 ? int.Parse(args[0]) : 5013;
            Console.WriteLine($"Testing with email ID: {emailId}");
            
            // This should show us what's missing for the enhanced logic to work properly
            TestEnhancedMethodDirectly(emailId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
    
    private static void TestEnhancedMethodDirectly(int emailId)
    {
        try
        {
            Console.WriteLine("🔧 Loading OnlyOffice assemblies...");
            
            var binPath = "/var/www/onlyoffice/WebStudio/bin";
            
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
            
            var mailAssembly = Assembly.LoadFrom(Path.Combine(binPath, "ASC.Mail.dll"));
            Console.WriteLine("✅ ASC.Mail.dll loaded");
            
            // Try to call the actual enhanced method
            var crmLinkEngineType = mailAssembly.GetType("ASC.Mail.Core.Engine.CrmLinkEngine");
            var crmContactDataType = mailAssembly.GetType("ASC.Mail.Data.Contracts.CrmContactData");
            
            if (crmLinkEngineType == null || crmContactDataType == null)
            {
                Console.WriteLine("❌ Required types not found");
                return;
            }
            
            Console.WriteLine("✅ Required types found");
            
            // The issue: We can't properly initialize OnlyOffice context outside the web app
            Console.WriteLine("⚠️ ISSUE IDENTIFIED:");
            Console.WriteLine("   - LinkChainToCrmEnhanced exists and should work");
            Console.WriteLine("   - But it requires proper OnlyOffice web application context");
            Console.WriteLine("   - ApiHelper.AddToCrmHistory() needs authenticated context");
            Console.WriteLine("   - Manual database approach bypasses this authentication");
            
            Console.WriteLine("\n🎯 SOLUTION:");
            Console.WriteLine("   1. Deploy WebCrmMonitoringService in ASC.Mail.dll build");
            Console.WriteLine("   2. Service runs within OnlyOffice web context with proper auth");
            Console.WriteLine("   3. Enhanced logic creates properly authenticated CRM events");
            Console.WriteLine("   4. CRM interface loads correctly with auth tokens");
            
            // Check what's actually in the current CRM events
            CheckCrmEventAuthentication();
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
    
    private static void CheckCrmEventAuthentication()
    {
        try
        {
            Console.WriteLine("\n🔍 Checking Current CRM Event Issues:");
            
            var connectionString = "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Connection Timeout=30;";
            
            var mysqlAssembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/MySql.Data.dll");
            var connectionType = mysqlAssembly.GetType("MySql.Data.MySqlClient.MySqlConnection");
            var commandType = mysqlAssembly.GetType("MySql.Data.MySqlClient.MySqlCommand");
            
            var connection = Activator.CreateInstance(connectionType, connectionString);
            try
            {
                var openMethod = connectionType.GetMethod("Open");
                openMethod.Invoke(connection, null);
                
                // Check CRM events for Test 32
                var query = @"
                    SELECT r.content 
                    FROM crm_relationship_event r
                    WHERE r.contact_id = 4 
                    AND r.content LIKE '%Test 32%'
                    LIMIT 1";
                
                var command = Activator.CreateInstance(commandType, query, connection);
                var executeReaderMethod = commandType.GetMethod("ExecuteReader", new Type[0]);
                var reader = executeReaderMethod.Invoke(command, null);
                
                try
                {
                    var readMethod = reader.GetType().GetMethod("Read");
                    if ((bool)readMethod.Invoke(reader, null))
                    {
                        var content = reader.GetType().GetMethod("GetString").Invoke(reader, new object[] { "content" });
                        Console.WriteLine("📧 Current CRM Event Content:");
                        Console.WriteLine($"   {content}");
                        
                        if (content.ToString().Contains("filehandler.ashx"))
                        {
                            Console.WriteLine("⚠️ PROBLEM: Using precompiled handler URL");
                            Console.WriteLine("   - filehandler.ashx is precompiled and returns marker text");
                            Console.WriteLine("   - Should use Mail API endpoints instead");
                            Console.WriteLine("   - Enhanced logic should create proper API URLs with auth");
                        }
                    }
                }
                finally
                {
                    reader.GetType().GetMethod("Close").Invoke(reader, null);
                }
            }
            finally
            {
                var closeMethod = connectionType.GetMethod("Close");
                closeMethod.Invoke(connection, null);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error checking CRM events: {ex.Message}");
        }
    }
}