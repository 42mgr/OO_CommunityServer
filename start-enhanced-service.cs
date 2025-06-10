using System;
using System.Reflection;
using System.IO;
using System.Linq;

/// <summary>
/// Check Service Status
/// Verifies if the enhanced CRM service is available and running
/// </summary>
class CheckServiceStatus
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("🔍 Checking Enhanced CRM Service Status");
            Console.WriteLine("======================================");
            
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
            
            // Check ASC.Mail.dll
            var mailDllPath = Path.Combine(binPath, "ASC.Mail.dll");
            var mailInfo = new FileInfo(mailDllPath);
            Console.WriteLine($"📄 ASC.Mail.dll: {mailInfo.Length} bytes, modified: {mailInfo.LastWriteTime}");
            
            // Load and inspect ASC.Mail assembly
            var mailAssembly = Assembly.LoadFrom(mailDllPath);
            Console.WriteLine($"✅ Loaded ASC.Mail.dll");
            
            // Check for our enhanced services
            Console.WriteLine("\n🔍 Checking for Enhanced CRM Services:");
            
            var allTypes = mailAssembly.GetTypes();
            Console.WriteLine($"Total types in assembly: {allTypes.Length}");
            
            // Look for WebCrmMonitoringService
            var webServiceType = Array.Find(allTypes, t => t.Name == "WebCrmMonitoringService");
            if (webServiceType != null)
            {
                Console.WriteLine("WebCrmMonitoringService found!");
                
                var isRunningProperty = webServiceType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Static);
                var getStatusMethod = webServiceType.GetMethod("GetStatus", BindingFlags.Public | BindingFlags.Static);
                var startMethod = webServiceType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                
                if (isRunningProperty != null)
                {
                    var isRunning = (bool)isRunningProperty.GetValue(null);
                    Console.WriteLine("   Current Running State: " + isRunning);
                    
                    if (!isRunning && startMethod != null)
                    {
                        Console.WriteLine("   Starting service...");
                        try
                        {
                            startMethod.Invoke(null, null);
                            Console.WriteLine("   START METHOD CALLED!");
                            
                            // Check status after start
                            var newRunning = (bool)isRunningProperty.GetValue(null);
                            Console.WriteLine("   New Running State: " + newRunning);
                            
                            if (getStatusMethod != null)
                            {
                                var status = getStatusMethod.Invoke(null, null);
                                Console.WriteLine("   Status: " + status);
                            }
                        }
                        catch (Exception startEx)
                        {
                            Console.WriteLine("   Error starting service: " + startEx.Message);
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("❌ WebCrmMonitoringService NOT found");
            }
            
            // Look for CrmEmailAutoLinkService (original)
            var originalServiceType = Array.Find(allTypes, t => t.Name == "CrmEmailAutoLinkService");
            if (originalServiceType != null)
            {
                Console.WriteLine("✅ CrmEmailAutoLinkService found!");
            }
            else
            {
                Console.WriteLine("❌ CrmEmailAutoLinkService NOT found");
            }
            
            // Check for CrmLinkEngine and enhanced method
            var crmLinkEngineType = Array.Find(allTypes, t => t.Name == "CrmLinkEngine");
            if (crmLinkEngineType != null)
            {
                Console.WriteLine("✅ CrmLinkEngine found!");
                
                var enhancedMethod = crmLinkEngineType.GetMethod("LinkChainToCrmEnhanced");
                if (enhancedMethod != null)
                {
                    Console.WriteLine("✅ LinkChainToCrmEnhanced method found!");
                    var parameters = enhancedMethod.GetParameters();
                    Console.WriteLine($"   Parameters: {parameters.Length}");
                    foreach (var param in parameters)
                    {
                        Console.WriteLine($"     - {param.ParameterType.Name} {param.Name}");
                    }
                }
                else
                {
                    Console.WriteLine("❌ LinkChainToCrmEnhanced method NOT found");
                }
            }
            else
            {
                Console.WriteLine("❌ CrmLinkEngine NOT found");
            }
            
            // Check recent emails
            Console.WriteLine("\n📧 Checking Recent Emails:");
            CheckRecentEmails();
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
    
    private static void CheckRecentEmails()
    {
        try
        {
            var connectionString = "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Connection Timeout=30;";
            
            var mysqlAssembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/MySql.Data.dll");
            var connectionType = mysqlAssembly.GetType("MySql.Data.MySqlClient.MySqlConnection");
            var commandType = mysqlAssembly.GetType("MySql.Data.MySqlClient.MySqlCommand");
                
                var connection = Activator.CreateInstance(connectionType, connectionString);
                try
                {
                    var openMethod = connectionType.GetMethod("Open");
                    openMethod.Invoke(connection, null);
                    
                    // Get recent emails
                    var query = @"
                        SELECT m.id, m.subject, m.from_text, m.date_received,
                               CASE WHEN l.entity_id IS NULL THEN 'NOT_LINKED' ELSE CONCAT('LINKED_TO_', l.entity_id) END as crm_status
                        FROM mail_mail m 
                        LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
                        WHERE m.date_received >= DATE_SUB(NOW(), INTERVAL 2 HOUR)
                        ORDER BY m.date_received DESC 
                        LIMIT 5";
                    
                    var command = Activator.CreateInstance(commandType, query, connection);
                    var executeReaderMethod = commandType.GetMethod("ExecuteReader", new Type[0]);
                    var reader = executeReaderMethod.Invoke(command, null);
                    
                    try
                    {
                        var readMethod = reader.GetType().GetMethod("Read");
                        Console.WriteLine("Recent emails (last 2 hours):");
                        
                        while ((bool)readMethod.Invoke(reader, null))
                        {
                            var id = reader.GetType().GetMethod("GetInt32").Invoke(reader, new object[] { "id" });
                            var subject = reader.GetType().GetMethod("GetString").Invoke(reader, new object[] { "subject" });
                            var fromText = reader.GetType().GetMethod("GetString").Invoke(reader, new object[] { "from_text" });
                            var dateReceived = reader.GetType().GetMethod("GetDateTime").Invoke(reader, new object[] { "date_received" });
                            var crmStatus = reader.GetType().GetMethod("GetString").Invoke(reader, new object[] { "crm_status" });
                            
                            Console.WriteLine($"  📧 ID:{id} - {subject} - From:{fromText} - {dateReceived} - CRM:{crmStatus}");
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
            Console.WriteLine($"❌ Error checking emails: {ex.Message}");
        }
    }
}