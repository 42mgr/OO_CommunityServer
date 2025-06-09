using System;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace ASC.Mail.Runtime
{
    /// <summary>
    /// Runtime CRM Email Monitor - can be started as a background thread
    /// </summary>
    public class RuntimeCrmMonitor
    {
        private static Timer _timer;
        private static bool _isRunning = false;
        private static string _connectionString = "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;Uid=onlyoffice_user;Pwd=onlyoffice_pass;CharSet=utf8;";
        private static DateTime _lastCheck = DateTime.UtcNow.AddMinutes(-5);
        
        public static void StartMonitoring()
        {
            Console.WriteLine("[CRM-MONITOR] 🚀 Starting runtime CRM email monitoring...");
            
            // Start timer to check every 30 seconds
            _timer = new Timer(CheckForNewEmails, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
            
            Console.WriteLine("[CRM-MONITOR] ✅ Monitoring started - checking every 30 seconds");
        }
        
        public static void StopMonitoring()
        {
            _timer?.Dispose();
            Console.WriteLine("[CRM-MONITOR] 🛑 Monitoring stopped");
        }
        
        private static void CheckForNewEmails(object state)
        {
            if (_isRunning) return;
            
            _isRunning = true;
            try
            {
                Console.WriteLine($"[CRM-MONITOR] 🔍 Checking for new emails since {_lastCheck:HH:mm:ss}");
                
                var processedCount = ProcessNewEmails();
                if (processedCount > 0)
                {
                    Console.WriteLine($"[CRM-MONITOR] ✅ Processed {processedCount} new emails");
                }
                
                _lastCheck = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRM-MONITOR] ❌ Error: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }
        
        private static int ProcessNewEmails()
        {
            var processedCount = 0;
            
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                
                // Get new emails since last check
                var query = @"
                    SELECT id, tenant, from_text, to_text, cc, subject
                    FROM mail_mail 
                    WHERE date_received > @lastCheck
                    AND folder IN (1, 2)
                    AND NOT EXISTS (
                        SELECT 1 FROM crm_relationship_event cre 
                        WHERE cre.entity_type = 0 AND cre.entity_id = mail_mail.id
                    )
                    LIMIT 20";
                
                var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@lastCheck", _lastCheck);
                
                var reader = cmd.ExecuteReader();
                var emails = new List<dynamic>();
                
                while (reader.Read())
                {
                    emails.Add(new {
                        Id = reader.GetInt32("id"),
                        Tenant = reader.GetInt32("tenant"),
                        From = reader.IsDBNull("from_text") ? "" : reader.GetString("from_text"),
                        To = reader.IsDBNull("to_text") ? "" : reader.GetString("to_text"),
                        Cc = reader.IsDBNull("cc") ? "" : reader.GetString("cc"),
                        Subject = reader.IsDBNull("subject") ? "" : reader.GetString("subject")
                    });
                }
                reader.Close();
                
                foreach (var email in emails)
                {
                    try
                    {
                        if (ProcessEmailForCrm(conn, email))
                        {
                            processedCount++;
                            Console.WriteLine($"[CRM-MONITOR] 📧 Processed email {email.Id}: {email.Subject}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CRM-MONITOR] ⚠️ Error processing email {email.Id}: {ex.Message}");
                    }
                }
            }
            
            return processedCount;
        }
        
        private static bool ProcessEmailForCrm(MySqlConnection conn, dynamic email)
        {
            // Extract email addresses
            var emailAddresses = ExtractEmailAddresses($"{email.From} {email.To} {email.Cc}");
            
            var matchCount = 0;
            
            foreach (var emailAddr in emailAddresses)
            {
                // Find matching CRM contacts
                var contactQuery = @"
                    SELECT c.id, c.display_name
                    FROM crm_contact c
                    INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
                    WHERE c.tenant_id = @tenant
                    AND ci.type = 1
                    AND LOWER(ci.data) = @email";
                
                var cmd = new MySqlCommand(contactQuery, conn);
                cmd.Parameters.AddWithValue("@tenant", email.Tenant);
                cmd.Parameters.AddWithValue("@email", emailAddr.ToLower());
                
                var contactReader = cmd.ExecuteReader();
                var contacts = new List<dynamic>();
                
                while (contactReader.Read())
                {
                    contacts.Add(new {
                        Id = contactReader.GetInt32("id"),
                        Name = contactReader.GetString("display_name")
                    });
                }
                contactReader.Close();
                
                // Create relationship events for matches
                foreach (var contact in contacts)
                {
                    CreateCrmRelationshipEvent(conn, email, contact, emailAddr);
                    matchCount++;
                    Console.WriteLine($"[CRM-MONITOR] 🎯 Linked email {email.Id} to {contact.Name} via {emailAddr}");
                }
            }
            
            // If no matches, create no-match event
            if (matchCount == 0)
            {
                CreateNoMatchEvent(conn, email);
                Console.WriteLine($"[CRM-MONITOR] 📭 No CRM matches for email {email.Id}");
            }
            
            return true;
        }
        
        private static List<string> ExtractEmailAddresses(string text)
        {
            var emails = new List<string>();
            var regex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.IgnoreCase);
            
            foreach (Match match in regex.Matches(text))
            {
                var email = match.Value.ToLower();
                if (!emails.Contains(email))
                    emails.Add(email);
            }
            
            return emails;
        }
        
        private static void CreateCrmRelationshipEvent(MySqlConnection conn, dynamic email, dynamic contact, string matchingEmail)
        {
            var insertQuery = @"
                INSERT INTO crm_relationship_event 
                (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
                VALUES (@contactId, 0, @entityId, @content, @createOn, 'system', @tenantId, -3, 0)";
            
            var cmd = new MySqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@contactId", contact.Id);
            cmd.Parameters.AddWithValue("@entityId", email.Id);
            cmd.Parameters.AddWithValue("@content", $"AUTO_LINKED via {matchingEmail}");
            cmd.Parameters.AddWithValue("@createOn", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@tenantId", email.Tenant);
            
            cmd.ExecuteNonQuery();
        }
        
        private static void CreateNoMatchEvent(MySqlConnection conn, dynamic email)
        {
            var insertQuery = @"
                INSERT INTO crm_relationship_event 
                (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
                VALUES (0, 0, @entityId, 'NO_CRM_MATCH', @createOn, 'system', @tenantId, -99, 0)";
            
            var cmd = new MySqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@entityId", email.Id);
            cmd.Parameters.AddWithValue("@createOn", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@tenantId", email.Tenant);
            
            cmd.ExecuteNonQuery();
        }
    }
    
    /// <summary>
    /// Simple launcher class
    /// </summary>
    public class CrmMonitorLauncher
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("🚀 ONLYOFFICE CRM Email Monitor - Runtime Edition");
            Console.WriteLine("================================================");
            
            try
            {
                RuntimeCrmMonitor.StartMonitoring();
                
                Console.WriteLine("📧 Monitoring started! Press 'q' to quit, 's' for status");
                
                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        RuntimeCrmMonitor.StopMonitoring();
                        break;
                    }
                    else if (key.KeyChar == 's' || key.KeyChar == 'S')
                    {
                        Console.WriteLine($"[STATUS] 📊 Monitoring active - Last check: {DateTime.Now:HH:mm:ss}");
                    }
                }
                
                Console.WriteLine("👋 Goodbye!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}