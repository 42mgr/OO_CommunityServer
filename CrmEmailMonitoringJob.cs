using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using log4net;
using Newtonsoft.Json;

namespace ASC.Mail.Enhanced
{
    /// <summary>
    /// Standalone database monitoring job for CRM email auto-linking
    /// Monitors mail_mail table for new entries and triggers CRM contact matching
    /// </summary>
    public class CrmEmailMonitoringJob
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CrmEmailMonitoringJob));
        private static Timer _monitoringTimer;
        private static DateTime _lastProcessedTime = DateTime.UtcNow.AddMinutes(-5);
        private static readonly string ConnectionString = GetConnectionString();
        private static bool _isRunning = false;
        
        public static void StartMonitoring()
        {
            Log.Info("🚀 CrmEmailMonitoringJob: Starting database monitoring for CRM auto-linking...");
            
            // Start monitoring every 30 seconds
            _monitoringTimer = new Timer(MonitorNewEmails, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
            
            Log.Info("✅ CrmEmailMonitoringJob: Database monitoring started successfully");
        }
        
        public static void StopMonitoring()
        {
            _monitoringTimer?.Dispose();
            Log.Info("🛑 CrmEmailMonitoringJob: Database monitoring stopped");
        }
        
        private static void MonitorNewEmails(object state)
        {
            if (_isRunning) return;
            
            _isRunning = true;
            try
            {
                Log.Debug("🔍 CrmEmailMonitoringJob: Checking for new emails since {0}", _lastProcessedTime);
                
                var newEmails = GetUnprocessedEmails();
                
                if (newEmails.Count > 0)
                {
                    Log.Info("📧 CrmEmailMonitoringJob: Found {0} new emails to process", newEmails.Count);
                    
                    foreach (var email in newEmails)
                    {
                        try
                        {
                            ProcessEmailForCrmLinking(email);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"❌ CrmEmailMonitoringJob: Error processing email {email.Id}: {ex.Message}", ex);
                        }
                    }
                }
                
                _lastProcessedTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Log.Error("❌ CrmEmailMonitoringJob: Error in monitoring cycle", ex);
            }
            finally
            {
                _isRunning = false;
            }
        }
        
        private static List<EmailData> GetUnprocessedEmails()
        {
            var emails = new List<EmailData>();
            
            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    
                    var query = @"
                        SELECT m.id, m.tenant, m.id_user, m.id_mailbox, m.from_text, m.to_text, m.cc, m.bcc, 
                               m.subject, m.date_received, m.folder, m.chain_id
                        FROM mail_mail m
                        WHERE m.date_received > @lastProcessed
                          AND m.folder IN (1, 2)  -- 1=Sent, 2=Inbox
                          AND NOT EXISTS (
                              SELECT 1 FROM crm_relationship_event cre 
                              WHERE cre.entity_type = 0 AND cre.entity_id = m.id
                          )
                        ORDER BY m.date_received ASC
                        LIMIT 100";
                    
                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@lastProcessed", _lastProcessedTime);
                        
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
                                    From = reader.GetString("from_text") ?? "",
                                    To = reader.GetString("to_text") ?? "",
                                    Cc = reader.GetString("cc") ?? "",
                                    Bcc = reader.GetString("bcc") ?? "",
                                    Subject = reader.GetString("subject") ?? "",
                                    DateReceived = reader.GetDateTime("date_received"),
                                    Folder = reader.GetInt32("folder"),
                                    ChainId = reader.IsDBNull("chain_id") ? null : reader.GetString("chain_id")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("❌ CrmEmailMonitoringJob: Error querying unprocessed emails", ex);
            }
            
            return emails;
        }
        
        private static void ProcessEmailForCrmLinking(EmailData email)
        {
            Log.Info("🔧 CrmEmailMonitoringJob: Processing email {0} from {1} for CRM linking", email.Id, email.From);
            
            try
            {
                // Extract all email addresses from the email
                var emailAddresses = ExtractEmailAddresses(email);
                
                if (emailAddresses.Count == 0)
                {
                    Log.Debug("⚠️ CrmEmailMonitoringJob: No valid email addresses found in email {0}", email.Id);
                    return;
                }
                
                // Find matching CRM contacts
                var matchingContacts = FindMatchingCrmContacts(email.TenantId, emailAddresses);
                
                if (matchingContacts.Count > 0)
                {
                    Log.Info("🎯 CrmEmailMonitoringJob: Found {0} matching CRM contacts for email {1}", matchingContacts.Count, email.Id);
                    
                    // Link email to CRM contacts
                    LinkEmailToCrmContacts(email, matchingContacts);
                }
                else
                {
                    Log.Debug("📭 CrmEmailMonitoringJob: No matching CRM contacts found for email {0}", email.Id);
                    
                    // Create a "no match" relationship event to avoid reprocessing
                    CreateNoMatchRelationshipEvent(email);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"❌ CrmEmailMonitoringJob: Error processing email {email.Id} for CRM linking", ex);
            }
        }
        
        private static List<string> ExtractEmailAddresses(EmailData email)
        {
            var emailAddresses = new List<string>();
            var emailRegex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.IgnoreCase);
            
            // Extract from all email fields
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
        
        private static List<CrmContactData> FindMatchingCrmContacts(int tenantId, List<string> emailAddresses)
        {
            var contacts = new List<CrmContactData>();
            
            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    
                    foreach (var emailAddr in emailAddresses)
                    {
                        var query = @"
                            SELECT DISTINCT c.id, c.first_name, c.last_name, c.company_name, c.display_name, c.is_company,
                                   ci.data as email_address
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
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("❌ CrmEmailMonitoringJob: Error finding matching CRM contacts", ex);
            }
            
            return contacts;
        }
        
        private static void LinkEmailToCrmContacts(EmailData email, List<CrmContactData> contacts)
        {
            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var contact in contacts)
                            {
                                // Link email chain to CRM contact
                                LinkEmailChainToCrmContact(connection, transaction, email, contact);
                                
                                // Create relationship event
                                CreateCrmRelationshipEvent(connection, transaction, email, contact);
                            }
                            
                            // Update chain to mark as CRM-linked
                            UpdateChainCrmStatus(connection, transaction, email);
                            
                            transaction.Commit();
                            
                            Log.Info("✅ CrmEmailMonitoringJob: Successfully linked email {0} to {1} CRM contacts", 
                                    email.Id, contacts.Count);
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"❌ CrmEmailMonitoringJob: Error linking email {email.Id} to CRM contacts", ex);
            }
        }
        
        private static void LinkEmailChainToCrmContact(MySqlConnection connection, MySqlTransaction transaction, 
                                                       EmailData email, CrmContactData contact)
        {
            if (string.IsNullOrEmpty(email.ChainId)) return;
            
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
        
        private static void CreateCrmRelationshipEvent(MySqlConnection connection, MySqlTransaction transaction,
                                                       EmailData email, CrmContactData contact)
        {
            var eventContent = JsonConvert.SerializeObject(new
            {
                from = email.From,
                to = email.To,
                subject = email.Subject,
                date = email.DateReceived,
                folder = email.Folder
            });
            
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
        
        private static void UpdateChainCrmStatus(MySqlConnection connection, MySqlTransaction transaction, EmailData email)
        {
            if (string.IsNullOrEmpty(email.ChainId)) return;
            
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
        
        private static void CreateNoMatchRelationshipEvent(EmailData email)
        {
            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    
                    // Create a dummy relationship event to mark as processed (no match)
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
            catch (Exception ex)
            {
                Log.Error($"❌ CrmEmailMonitoringJob: Error creating no-match event for email {email.Id}", ex);
            }
        }
        
        private static string GetConnectionString()
        {
            // Default connection string for Docker setup
            // Modify this based on your actual database configuration
            return "Server=localhost;Database=onlyoffice;Uid=onlyoffice_user;Pwd=onlyoffice_pass;CharSet=utf8;";
        }
    }
    
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
}