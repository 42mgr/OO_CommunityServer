using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using MySql.Data.MySqlClient;

/// <summary>
/// Database Email Monitor Service
/// Monitors mail_mail table for new unlinked emails and triggers CRM auto-linking via API
/// </summary>
class EmailMonitorService
{
    private static readonly string ConnectionString = "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Pooling=true;Character Set=utf8;Connection Timeout=30;";
    private static readonly string OnlyOfficeApiUrl = "http://localhost:1180/CrmAutoLinkApi.ashx";
    private static DateTime _lastProcessedTime = DateTime.UtcNow.AddMinutes(-5);
    private static readonly object _lockObject = new object();
    
    static void Main()
    {
        Console.WriteLine("🔍 Starting Email Monitor Service for CRM Auto-Linking...");
        Console.WriteLine($"⏰ Last processed time: {_lastProcessedTime:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"🌐 OnlyOffice API URL: {OnlyOfficeApiUrl}");
        Console.WriteLine($"🗄️ Database: {ConnectionString.Split(';')[0]}");
        
        try
        {
            // Test database connection
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                Console.WriteLine("✅ Database connection successful");
            }
            
            // Start monitoring loop
            Console.WriteLine("🚀 Starting email monitoring loop (30-second intervals)...");
            Console.WriteLine("Press Ctrl+C to stop");
            
            while (true)
            {
                try
                {
                    ProcessNewEmails();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error in monitoring cycle: {ex.Message}");
                }
                
                Thread.Sleep(30000); // Wait 30 seconds
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    private static void ProcessNewEmails()
    {
        lock (_lockObject)
        {
            var now = DateTime.UtcNow;
            Console.WriteLine($"\n🔍 [{now:HH:mm:ss}] Checking for new emails since {_lastProcessedTime:HH:mm:ss}...");
            
            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    
                    // Query for unlinked emails in the last 5 minutes
                    var query = @"
                        SELECT m.id, m.id_user, m.from_text, m.to_text, m.cc, m.subject, m.date_received, m.tenant
                        FROM mail_mail m
                        LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
                        WHERE m.tenant = 1 
                        AND m.date_received >= @lastProcessed 
                        AND m.date_received <= @now
                        AND m.folder IN (1, 2)
                        AND l.id_chain IS NULL
                        ORDER BY m.date_received DESC
                        LIMIT 50";
                    
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@lastProcessed", _lastProcessedTime);
                        cmd.Parameters.AddWithValue("@now", now);
                        
                        var unprocessedEmails = new List<EmailInfo>();
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                unprocessedEmails.Add(new EmailInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    UserId = reader.GetString("id_user"),
                                    From = reader.GetString("from_text"),
                                    To = reader.GetString("to_text"),
                                    Cc = reader.IsDBNull(reader.GetOrdinal("cc")) ? "" : reader.GetString("cc"),
                                    Subject = reader.GetString("subject"),
                                    DateReceived = reader.GetDateTime("date_received"),
                                    Tenant = reader.GetInt32("tenant")
                                });
                            }
                        }
                        
                        if (unprocessedEmails.Count == 0)
                        {
                            Console.WriteLine("📭 No new unlinked emails found");
                        }
                        else
                        {
                            Console.WriteLine($"📧 Found {unprocessedEmails.Count} new unlinked emails");
                            
                            foreach (var email in unprocessedEmails)
                            {
                                ProcessSingleEmail(email);
                            }
                        }
                    }
                }
                
                _lastProcessedTime = now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Database error: {ex.Message}");
            }
        }
    }
    
    private static void ProcessSingleEmail(EmailInfo email)
    {
        try
        {
            Console.WriteLine($"🔧 Processing email {email.Id}: '{email.Subject}' from {email.From}");
            
            // Check if email addresses contain potential CRM matches
            var allEmails = new List<string>();
            if (!string.IsNullOrEmpty(email.From)) allEmails.AddRange(ExtractEmailAddresses(email.From));
            if (!string.IsNullOrEmpty(email.To)) allEmails.AddRange(ExtractEmailAddresses(email.To));
            if (!string.IsNullOrEmpty(email.Cc)) allEmails.AddRange(ExtractEmailAddresses(email.Cc));
            
            // Check if any email addresses match known CRM contacts
            bool hasCrmMatch = false;
            foreach (var emailAddr in allEmails.Distinct())
            {
                if (HasCrmContact(emailAddr))
                {
                    hasCrmMatch = true;
                    Console.WriteLine($"✅ Found CRM match for email address: {emailAddr}");
                    break;
                }
            }
            
            if (!hasCrmMatch)
            {
                Console.WriteLine($"📭 No CRM contacts found for email {email.Id}");
                return;
            }
            
            // Call OnlyOffice API to trigger CRM linking
            var success = CallCrmLinkingApi(email.Id, email.Tenant, email.UserId);
            
            if (success)
            {
                Console.WriteLine($"🎯 Successfully triggered CRM auto-linking for email {email.Id}");
            }
            else
            {
                Console.WriteLine($"⚠️ Failed to trigger CRM auto-linking for email {email.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing email {email.Id}: {ex.Message}");
        }
    }
    
    private static List<string> ExtractEmailAddresses(string emailField)
    {
        var emails = new List<string>();
        if (string.IsNullOrEmpty(emailField)) return emails;
        
        try
        {
            // Simple email extraction - look for patterns like "email@domain.com"
            var parts = emailField.Split(new char[] { ',', ';', ' ', '<', '>', '"' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Contains("@") && part.Contains("."))
                {
                    var cleanEmail = part.Trim().ToLower();
                    if (!emails.Contains(cleanEmail))
                    {
                        emails.Add(cleanEmail);
                    }
                }
            }
        }
        catch
        {
            // If parsing fails, ignore
        }
        
        return emails;
    }
    
    private static bool HasCrmContact(string emailAddress)
    {
        try
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                
                var query = @"
                    SELECT COUNT(*) 
                    FROM crm_contact c
                    INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
                    WHERE ci.type = 1 
                    AND c.status_id != 1 
                    AND LOWER(ci.data) = @email";
                
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@email", emailAddress.ToLower());
                    var count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }
        catch
        {
            return false;
        }
    }
    
    private static bool CallCrmLinkingApi(int emailId, int tenantId, string userId)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                
                var url = $"{OnlyOfficeApiUrl}?emailId={emailId}&tenantId={tenantId}&userId={userId}";
                
                var response = client.GetAsync(url).Result;
                var content = response.Content.ReadAsStringAsync().Result;
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"📞 API call successful: {content.Trim()}");
                    return content.Contains("SUCCESS") || content.Contains("✅");
                }
                else
                {
                    Console.WriteLine($"📞 API call failed ({response.StatusCode}): {content}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"📞 API call error: {ex.Message}");
            return false;
        }
    }
}

public class EmailInfo
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string From { get; set; }
    public string To { get; set; }
    public string Cc { get; set; }
    public string Subject { get; set; }
    public DateTime DateReceived { get; set; }
    public int Tenant { get; set; }
}