using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

class CrmMonitoringTest
{
    private static string _connectionString = "Server=localhost;Port=3306;Database=onlyoffice;Uid=onlyoffice_user;Pwd=onlyoffice_pass;CharSet=utf8;";
    
    static void Main()
    {
        Console.WriteLine("🚀 Testing CRM Email Monitoring");
        Console.WriteLine("===============================");
        
        try
        {
            // Test database connection
            TestConnection();
            
            // Process unprocessed emails
            ProcessEmails();
            
            // Show results
            ShowResults();
            
            Console.WriteLine("\n✅ Test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
        }
    }
    
    static void TestConnection()
    {
        Console.WriteLine("🔌 Testing database connection...");
        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new MySqlCommand("SELECT COUNT(*) FROM mail_mail WHERE id >= 2001", conn);
            var count = cmd.ExecuteScalar();
            Console.WriteLine($"✅ Found {count} test emails");
        }
    }
    
    static void ProcessEmails()
    {
        Console.WriteLine("\n📧 Processing emails for CRM linking...");
        
        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            
            // Get unprocessed emails
            var emailQuery = @"
                SELECT id, from_text, to_text, cc, subject, tenant
                FROM mail_mail 
                WHERE id >= 2001 
                AND folder IN (1, 2)
                AND NOT EXISTS (
                    SELECT 1 FROM crm_relationship_event cre 
                    WHERE cre.entity_type = 0 AND cre.entity_id = mail_mail.id
                )";
            
            var cmd = new MySqlCommand(emailQuery, conn);
            var reader = cmd.ExecuteReader();
            var emails = new List<dynamic>();
            
            while (reader.Read())
            {
                emails.Add(new {
                    Id = reader.GetInt32("id"),
                    From = reader.IsDBNull("from_text") ? "" : reader.GetString("from_text"),
                    To = reader.IsDBNull("to_text") ? "" : reader.GetString("to_text"),
                    Cc = reader.IsDBNull("cc") ? "" : reader.GetString("cc"),
                    Subject = reader.IsDBNull("subject") ? "" : reader.GetString("subject"),
                    Tenant = reader.GetInt32("tenant")
                });
            }
            reader.Close();
            
            Console.WriteLine($"📋 Found {emails.Count} unprocessed emails");
            
            foreach (var email in emails)
            {
                ProcessSingleEmail(conn, email);
            }
        }
    }
    
    static void ProcessSingleEmail(MySqlConnection conn, dynamic email)
    {
        Console.WriteLine($"\n🔧 Processing email {email.Id}: {email.Subject}");
        
        // Extract email addresses
        var emailAddresses = ExtractEmails($"{email.From} {email.To} {email.Cc}");
        Console.WriteLine($"📋 Extracted emails: {string.Join(", ", emailAddresses)}");
        
        var matchCount = 0;
        
        foreach (var emailAddr in emailAddresses)
        {
            // Find matching CRM contacts
            var contactQuery = @"
                SELECT c.id, c.display_name, ci.data
                FROM crm_contact c
                INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
                WHERE c.tenant_id = @tenant
                AND ci.type = 1
                AND LOWER(ci.data) = @email";
            
            var cmd = new MySqlCommand(contactQuery, conn);
            cmd.Parameters.AddWithValue("@tenant", email.Tenant);
            cmd.Parameters.AddWithValue("@email", emailAddr.ToLower());
            
            var contactReader = cmd.ExecuteReader();
            while (contactReader.Read())
            {
                var contactId = contactReader.GetInt32("id");
                var contactName = contactReader.GetString("display_name");
                var matchingEmail = contactReader.GetString("data");
                
                Console.WriteLine($"🎯 Found match: {contactName} ({matchingEmail})");
                matchCount++;
            }
            contactReader.Close();
            
            if (matchCount > 0)
            {
                // Create relationship event for each match
                CreateRelationshipEvent(conn, email, contactId: matchCount, emailAddr);
            }
        }
        
        if (matchCount == 0)
        {
            Console.WriteLine("📭 No CRM matches found");
            CreateNoMatchEvent(conn, email);
        }
    }
    
    static List<string> ExtractEmails(string text)
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
    
    static void CreateRelationshipEvent(MySqlConnection conn, dynamic email, int contactId, string matchingEmail)
    {
        try
        {
            var insertQuery = @"
                INSERT INTO crm_relationship_event 
                (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
                VALUES (@contactId, 0, @entityId, @content, @createOn, 'admin', @tenantId, -3, 0)";
            
            var cmd = new MySqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@contactId", contactId);
            cmd.Parameters.AddWithValue("@entityId", email.Id);
            cmd.Parameters.AddWithValue("@content", $"CRM_AUTO_LINKED via {matchingEmail}");
            cmd.Parameters.AddWithValue("@createOn", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@tenantId", email.Tenant);
            
            cmd.ExecuteNonQuery();
            Console.WriteLine($"✅ Created CRM relationship event");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error creating relationship: {ex.Message}");
        }
    }
    
    static void CreateNoMatchEvent(MySqlConnection conn, dynamic email)
    {
        try
        {
            var insertQuery = @"
                INSERT INTO crm_relationship_event 
                (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
                VALUES (0, 0, @entityId, 'NO_CRM_MATCH', @createOn, 'admin', @tenantId, -99, 0)";
            
            var cmd = new MySqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@entityId", email.Id);
            cmd.Parameters.AddWithValue("@createOn", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@tenantId", email.Tenant);
            
            cmd.ExecuteNonQuery();
            Console.WriteLine($"📭 Marked as no CRM match");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error creating no-match event: {ex.Message}");
        }
    }
    
    static void ShowResults()
    {
        Console.WriteLine("\n📊 Results Summary");
        Console.WriteLine("==================");
        
        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            
            // Count processed emails
            var processedQuery = @"
                SELECT COUNT(*) FROM crm_relationship_event 
                WHERE entity_type = 0 AND entity_id >= 2001";
            var cmd = new MySqlCommand(processedQuery, conn);
            var processed = cmd.ExecuteScalar();
            
            Console.WriteLine($"📧 Emails processed: {processed}");
            
            // Count CRM matches
            var matchQuery = @"
                SELECT COUNT(*) FROM crm_relationship_event 
                WHERE entity_type = 0 AND entity_id >= 2001 AND category_id = -3";
            cmd = new MySqlCommand(matchQuery, conn);
            var matches = cmd.ExecuteScalar();
            
            Console.WriteLine($"🎯 CRM matches found: {matches}");
            
            // Show details
            var detailQuery = @"
                SELECT cre.entity_id, cre.contact_id, cre.content, c.display_name
                FROM crm_relationship_event cre
                LEFT JOIN crm_contact c ON cre.contact_id = c.id
                WHERE cre.entity_type = 0 AND cre.entity_id >= 2001
                ORDER BY cre.entity_id";
            
            cmd = new MySqlCommand(detailQuery, conn);
            var reader = cmd.ExecuteReader();
            
            Console.WriteLine("\n📋 Processing Details:");
            while (reader.Read())
            {
                var emailId = reader.GetInt32("entity_id");
                var contactId = reader.GetInt32("contact_id");
                var content = reader.GetString("content");
                var contactName = reader.IsDBNull("display_name") ? "No Match" : reader.GetString("display_name");
                
                Console.WriteLine($"  Email {emailId} → {contactName} ({content})");
            }
        }
    }
}