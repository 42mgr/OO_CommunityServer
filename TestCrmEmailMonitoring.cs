using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;

namespace ASC.Mail.Enhanced.Test
{
    /// <summary>
    /// Console application to test the CRM Email Monitoring Job
    /// Run this to verify the monitoring is working correctly
    /// </summary>
    class TestCrmEmailMonitoring
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TestCrmEmailMonitoring));
        
        static async Task Main(string[] args)
        {
            try
            {
                // Configure logging
                XmlConfigurator.Configure();
                
                Console.WriteLine("🚀 CRM Email Monitoring Test Application");
                Console.WriteLine("=========================================");
                Console.WriteLine();
                
                Log.Info("🚀 TestCrmEmailMonitoring: Starting test application...");
                
                // Test database connection
                await TestDatabaseConnection();
                
                // Start monitoring
                Console.WriteLine("📧 Starting CRM Email Monitoring Job...");
                CrmEmailMonitoringJob.StartMonitoring();
                
                Console.WriteLine("✅ Monitoring started successfully!");
                Console.WriteLine("⏰ The job will check for new emails every 30 seconds.");
                Console.WriteLine("📊 Check the logs for processing details.");
                Console.WriteLine();
                Console.WriteLine("Commands:");
                Console.WriteLine("  [q] - Quit");
                Console.WriteLine("  [s] - Show status");
                Console.WriteLine("  [t] - Test query");
                Console.WriteLine();
                
                // Main loop
                while (true)
                {
                    var key = Console.ReadKey(true);
                    
                    switch (key.KeyChar.ToString().ToLower())
                    {
                        case "q":
                            Console.WriteLine("🛑 Stopping monitoring...");
                            CrmEmailMonitoringJob.StopMonitoring();
                            Console.WriteLine("✅ Stopped. Goodbye!");
                            return;
                            
                        case "s":
                            ShowStatus();
                            break;
                            
                        case "t":
                            await TestQuery();
                            break;
                            
                        default:
                            Console.WriteLine($"Unknown command: {key.KeyChar}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Critical error: {ex.Message}");
                Log.Error("❌ TestCrmEmailMonitoring: Critical error", ex);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
        
        private static async Task TestDatabaseConnection()
        {
            Console.WriteLine("🔌 Testing database connection...");
            
            try
            {
                using (var connection = new MySql.Data.MySqlClient.MySqlConnection(GetConnectionString()))
                {
                    await connection.OpenAsync();
                    
                    var command = new MySql.Data.MySqlClient.MySqlCommand("SELECT COUNT(*) FROM mail_mail", connection);
                    var mailCount = await command.ExecuteScalarAsync();
                    
                    command = new MySql.Data.MySqlClient.MySqlCommand("SELECT COUNT(*) FROM crm_contact", connection);
                    var contactCount = await command.ExecuteScalarAsync();
                    
                    Console.WriteLine($"✅ Database connected successfully!");
                    Console.WriteLine($"📧 Total emails in database: {mailCount}");
                    Console.WriteLine($"👤 Total CRM contacts: {contactCount}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Database connection failed: {ex.Message}");
                Log.Error("❌ TestCrmEmailMonitoring: Database connection failed", ex);
                throw;
            }
        }
        
        private static void ShowStatus()
        {
            Console.WriteLine();
            Console.WriteLine("📊 CRM Email Monitoring Status");
            Console.WriteLine("==============================");
            Console.WriteLine($"⏰ Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"🔄 Service status: Running");
            Console.WriteLine($"📧 Monitoring for new emails every 30 seconds");
            Console.WriteLine();
        }
        
        private static async Task TestQuery()
        {
            Console.WriteLine();
            Console.WriteLine("🔍 Testing query for unprocessed emails...");
            
            try
            {
                using (var connection = new MySql.Data.MySqlClient.MySqlConnection(GetConnectionString()))
                {
                    await connection.OpenAsync();
                    
                    var query = @"
                        SELECT COUNT(*) as unprocessed_count
                        FROM mail_mail m
                        WHERE m.folder IN (1, 2)  -- 1=Sent, 2=Inbox
                          AND NOT EXISTS (
                              SELECT 1 FROM crm_relationship_event cre 
                              WHERE cre.entity_type = 0 AND cre.entity_id = m.id
                          )";
                    
                    var command = new MySql.Data.MySqlClient.MySqlCommand(query, connection);
                    var unprocessedCount = await command.ExecuteScalarAsync();
                    
                    Console.WriteLine($"📧 Unprocessed emails found: {unprocessedCount}");
                    
                    // Test recent emails
                    var recentQuery = @"
                        SELECT COUNT(*) as recent_count
                        FROM mail_mail m
                        WHERE m.folder IN (1, 2)
                          AND m.date_received > DATE_SUB(NOW(), INTERVAL 1 HOUR)";
                    
                    command = new MySql.Data.MySqlClient.MySqlCommand(recentQuery, connection);
                    var recentCount = await command.ExecuteScalarAsync();
                    
                    Console.WriteLine($"⏰ Recent emails (last hour): {recentCount}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Query test failed: {ex.Message}");
                Log.Error("❌ TestCrmEmailMonitoring: Query test failed", ex);
            }
        }
        
        private static string GetConnectionString()
        {
            // You can modify this connection string based on your Docker setup
            return "Server=localhost;Port=3306;Database=onlyoffice;Uid=onlyoffice_user;Pwd=onlyoffice_pass;CharSet=utf8;Connection Timeout=30;";
        }
    }
}