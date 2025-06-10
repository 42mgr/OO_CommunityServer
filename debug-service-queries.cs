using System;
using System.Reflection;

class DebugServiceQueries
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔍 Debugging CRM auto-link service queries...");
            
            // Test the same query the service uses to find unprocessed emails
            var connectionString = "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Pooling=true;Character Set=utf8;AutoEnlist=false;SSL Mode=none;AllowPublicKeyRetrieval=true;Connection Timeout=30;Maximum Pool Size=300;";
            
            // Load the ASC.Common assembly for database access
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Common.dll");
            var dbManagerType = assembly.GetType("ASC.Common.Data.DbManager");
            
            Console.WriteLine("Testing database query for recent emails...");
            
            // Check what the service would find with current logic
            var now = DateTime.UtcNow;
            var lastProcessed = now.AddMinutes(-30); // Check last 30 minutes
            
            Console.WriteLine($"Now: {now:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"LastProcessed: {lastProcessed:yyyy-MM-dd HH:mm:ss} UTC");
            
            // Manual SQL query to debug
            var testQuery = @"
                SELECT m.id, m.id_user, m.from_text, m.to_text, m.date_received,
                       CASE WHEN l.id_chain IS NULL THEN 'NOT_LINKED' ELSE 'LINKED' END as link_status
                FROM mail_mail m
                LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
                WHERE m.tenant = 1 
                AND m.date_received >= DATE_SUB(NOW(), INTERVAL 30 MINUTE)
                AND m.folder IN (1, 2)
                ORDER BY m.date_received DESC
                LIMIT 10";
            
            Console.WriteLine("Query to check:");
            Console.WriteLine(testQuery);
            Console.WriteLine("\nThis should show what emails the service would find...");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}