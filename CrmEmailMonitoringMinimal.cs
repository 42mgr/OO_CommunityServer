using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace ASC.Mail.Enhanced.Minimal
{
    /// <summary>
    /// Minimal, standalone CRM Email Monitoring implementation
    /// This is a simplified version that can be easily tested and integrated
    /// </summary>
    public class CrmEmailMonitoringMinimal
    {
        private readonly string _connectionString;
        private readonly bool _debugMode;
        
        public CrmEmailMonitoringMinimal(string connectionString, bool debugMode = true)
        {
            _connectionString = connectionString;
            _debugMode = debugMode;
        }
        
        /// <summary>
        /// Process all unprocessed emails for CRM linking
        /// </summary>
        public void ProcessUnprocessedEmails()
        {
            Log("🚀 Starting CRM email processing...");
            
            try
            {
                var unprocessedEmails = GetUnprocessedEmails();
                Log($"📧 Found {unprocessedEmails.Count} unprocessed emails");
                
                int processedCount = 0;
                int linkedCount = 0;
                
                foreach (var email in unprocessedEmails)
                {
                    try
                    {
                        var linkedContacts = ProcessEmailForCrmLinking(email);
                        processedCount++;
                        
                        if (linkedContacts > 0)
                        {
                            linkedCount++;
                            Log($"✅ Email {email.Id} linked to {linkedContacts} CRM contacts");
                        }
                        else
                        {
                            Log($"📭 Email {email.Id} - no CRM matches found");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Error processing email {email.Id}: {ex.Message}");
                    }
                }
                
                Log($"🎯 Processing complete: {processedCount} emails processed, {linkedCount} linked to CRM");
            }
            catch (Exception ex)
            {
                Log($"❌ Critical error in ProcessUnprocessedEmails: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Get emails that haven't been processed for CRM linking
        /// </summary>
        private List<EmailData> GetUnprocessedEmails()
        {
            var emails = new List<EmailData>();
            
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                
                var query = @"
                    SELECT m.id, m.tenant, m.id_user, m.id_mailbox, m.from_text, m.to_text, 
                           m.cc, m.bcc, m.subject, m.date_received, m.folder, m.chain_id
                    FROM mail_mail m
                    WHERE m.folder IN (1, 2)  -- 1=Sent, 2=Inbox
                      AND NOT EXISTS (
                          SELECT 1 FROM crm_relationship_event cre 
                          WHERE cre.entity_type = 0 AND cre.entity_id = m.id
                      )
                    ORDER BY m.date_received DESC
                    LIMIT 50";
                
                using (var command = new MySqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        emails.Add(new EmailData
                        {
                            Id = reader.GetInt32("id"),
                            TenantId = reader.GetInt32("tenant"),
                            UserId = reader.GetString("id_user"),
                            MailboxId = reader.GetInt32("id_mailbox"),
                            From = reader.IsDBNull("from_text") ? "" : reader.GetString("from_text"),
                            To = reader.IsDBNull("to_text") ? "" : reader.GetString("to_text"),
                            Cc = reader.IsDBNull("cc") ? "" : reader.GetString("cc"),
                            Bcc = reader.IsDBNull("bcc") ? "" : reader.GetString("bcc"),
                            Subject = reader.IsDBNull("subject") ? "" : reader.GetString("subject"),
                            DateReceived = reader.GetDateTime("date_received"),
                            Folder = reader.GetInt32("folder"),
                            ChainId = reader.IsDBNull("chain_id") ? null : reader.GetString("chain_id")
                        });
                    }
                }
            }
            
            return emails;
        }
        
        /// <summary>
        /// Process a single email for CRM linking
        /// </summary>
        private int ProcessEmailForCrmLinking(EmailData email)
        {
            Log($"🔧 Processing email {email.Id}: {email.Subject}");
            
            // Extract email addresses
            var emailAddresses = ExtractEmailAddresses(email);
            if (emailAddresses.Count == 0)
            {
                CreateNoMatchRelationshipEvent(email);
                return 0;
            }
            
            Log($"📋 Extracted {emailAddresses.Count} email addresses: {string.Join(", ", emailAddresses)}");
            
            // Find matching CRM contacts
            var matchingContacts = FindMatchingCrmContacts(email.TenantId, emailAddresses);
            
            if (matchingContacts.Count > 0)
            {
                Log($"🎯 Found {matchingContacts.Count} matching CRM contacts");
                LinkEmailToCrmContacts(email, matchingContacts);
                return matchingContacts.Count;
            }
            else
            {
                Log($"📭 No matching CRM contacts found");
                CreateNoMatchRelationshipEvent(email);
                return 0;
            }
        }
        
        /// <summary>
        /// Extract email addresses from email fields
        /// </summary>
        private List<string> ExtractEmailAddresses(EmailData email)
        {
            var emailAddresses = new List<string>();
            var emailRegex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.IgnoreCase);
            
            var allText = $"{email.From} {email.To} {email.Cc} {email.Bcc}";
            var matches = emailRegex.Matches(allText);
            
            foreach (Match match in matches)
            {
                var emailAddr = match.Value.ToLower();
                if (!emailAddresses.Contains(emailAddr))
                {
                    emailAddresses.Add(emailAddr);
                }
            }
            
            return emailAddresses;
        }
        
        /// <summary>
        /// Find CRM contacts matching the email addresses
        /// </summary>
        private List<CrmContactData> FindMatchingCrmContacts(int tenantId, List<string> emailAddresses)
        {
            var contacts = new List<CrmContactData>();
            
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                
                foreach (var emailAddr in emailAddresses)
                {
                    var query = @"
                        SELECT DISTINCT c.id, c.first_name, c.last_name, c.company_name, 
                               c.display_name, c.is_company, ci.data as email_address
                        FROM crm_contact c
                        INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
                        WHERE c.tenant_id = @tenantId
                          AND ci.type = 1  -- Email type
                          AND LOWER(ci.data) = @emailAddress";
                    
                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tenantId", tenantId);
                        command.Parameters.AddWithValue("@emailAddress", emailAddr);
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var contact = new CrmContactData
                                {
                                    Id = reader.GetInt32("id"),
                                    FirstName = reader.IsDBNull("first_name") ? "" : reader.GetString("first_name"),
                                    LastName = reader.IsDBNull("last_name") ? "" : reader.GetString("last_name"),
                                    CompanyName = reader.IsDBNull("company_name") ? "" : reader.GetString("company_name"),
                                    DisplayName = reader.IsDBNull("display_name") ? "" : reader.GetString("display_name"),
                                    IsCompany = reader.GetBoolean("is_company"),
                                    EmailAddress = reader.GetString("email_address")
                                };
                                
                                if (!contacts.Exists(c => c.Id == contact.Id))
                                {
                                    contacts.Add(contact);
                                    Log($"🎯 Found matching contact: {contact.DisplayName} ({contact.EmailAddress})");
                                }
                            }
                        }
                    }
                }
            }
            
            return contacts;
        }
        
        /// <summary>
        /// Link email to CRM contacts
        /// </summary>
        private void LinkEmailToCrmContacts(EmailData email, List<CrmContactData> contacts)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var contact in contacts)
                        {
                            // Create relationship event
                            CreateCrmRelationshipEvent(connection, transaction, email, contact);
                            
                            // Link email chain to CRM contact (if chain exists)
                            if (!string.IsNullOrEmpty(email.ChainId))
                            {
                                LinkEmailChainToCrmContact(connection, transaction, email, contact);
                            }
                        }
                        
                        // Update chain to mark as CRM-linked
                        if (!string.IsNullOrEmpty(email.ChainId))
                        {
                            UpdateChainCrmStatus(connection, transaction, email);
                        }
                        
                        transaction.Commit();
                        Log($"✅ Successfully linked email {email.Id} to {contacts.Count} CRM contacts");
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
        
        /// <summary>
        /// Create CRM relationship event
        /// </summary>
        private void CreateCrmRelationshipEvent(MySqlConnection connection, MySqlTransaction transaction,
                                                EmailData email, CrmContactData contact)
        {
            var eventContent = $"{{\"from\":\"{email.From}\",\"to\":\"{email.To}\",\"subject\":\"{email.Subject}\",\"date\":\"{email.DateReceived:yyyy-MM-dd HH:mm:ss}\",\"folder\":{email.Folder}}}";
            
            var query = @"
                INSERT INTO crm_relationship_event 
                (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
                VALUES (@contactId, 0, @entityId, @content, @createOn, @createBy, @tenantId, -3, 0)";
            
            using (var command = new MySqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@contactId", contact.Id);
                command.Parameters.AddWithValue("@entityId", email.Id);
                command.Parameters.AddWithValue("@content", eventContent);
                command.Parameters.AddWithValue("@createOn", DateTime.UtcNow);
                command.Parameters.AddWithValue("@createBy", email.UserId);
                command.Parameters.AddWithValue("@tenantId", email.TenantId);
                
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Link email chain to CRM contact
        /// </summary>
        private void LinkEmailChainToCrmContact(MySqlConnection connection, MySqlTransaction transaction,
                                               EmailData email, CrmContactData contact)
        {
            var query = @"
                INSERT IGNORE INTO mail_chain_x_crm_entity 
                (id_tenant, id_mailbox, id_chain, entity_id, entity_type)
                VALUES (@tenantId, @mailboxId, @chainId, @contactId, 0)";
            
            using (var command = new MySqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@tenantId", email.TenantId);
                command.Parameters.AddWithValue("@mailboxId", email.MailboxId);
                command.Parameters.AddWithValue("@chainId", email.ChainId);
                command.Parameters.AddWithValue("@contactId", contact.Id);
                
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Update chain CRM status
        /// </summary>
        private void UpdateChainCrmStatus(MySqlConnection connection, MySqlTransaction transaction, EmailData email)
        {
            var query = @"
                UPDATE mail_chain 
                SET is_crm_chain = 1 
                WHERE id = @chainId AND id_mailbox = @mailboxId AND tenant = @tenantId";
            
            using (var command = new MySqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@chainId", email.ChainId);
                command.Parameters.AddWithValue("@mailboxId", email.MailboxId);
                command.Parameters.AddWithValue("@tenantId", email.TenantId);
                
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Create no-match relationship event
        /// </summary>
        private void CreateNoMatchRelationshipEvent(EmailData email)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                
                var query = @"
                    INSERT INTO crm_relationship_event 
                    (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
                    VALUES (0, 0, @entityId, 'NO_CRM_MATCH', @createOn, @createBy, @tenantId, -99, 0)";
                
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@entityId", email.Id);
                    command.Parameters.AddWithValue("@createOn", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@createBy", email.UserId);
                    command.Parameters.AddWithValue("@tenantId", email.TenantId);
                    
                    command.ExecuteNonQuery();
                }
            }
        }
        
        /// <summary>
        /// Simple logging
        /// </summary>
        private void Log(string message)
        {
            if (_debugMode)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            }
        }
    }
    
    // Data classes (same as before)
    public class EmailData
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string UserId { get; set; }
        public int MailboxId { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string Subject { get; set; }
        public DateTime DateReceived { get; set; }
        public int Folder { get; set; }
        public string ChainId { get; set; }
    }
    
    public class CrmContactData
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string CompanyName { get; set; }
        public string DisplayName { get; set; }
        public bool IsCompany { get; set; }
        public string EmailAddress { get; set; }
    }
    
    /// <summary>
    /// Simple test program
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🚀 CRM Email Monitoring - Minimal Test");
            Console.WriteLine("=====================================");
            
            // Update this connection string for your environment
            var connectionString = "Server=localhost;Port=3306;Database=onlyoffice;Uid=onlyoffice_user;Pwd=onlyoffice_pass;CharSet=utf8;";
            
            try
            {
                var monitor = new CrmEmailMonitoringMinimal(connectionString, debugMode: true);
                monitor.ProcessUnprocessedEmails();
                
                Console.WriteLine("\n✅ Processing completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}