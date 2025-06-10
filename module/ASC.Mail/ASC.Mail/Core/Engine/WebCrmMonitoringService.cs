/*
 * Web CRM Monitoring Service
 * Background service that runs within ASC.Mail.dll web context
 * Monitors for new emails and triggers enhanced CRM linking
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Mail.Data.Contracts;
using ASC.Mail.Utils;
using MySql.Data.MySqlClient;

namespace ASC.Mail.Core.Engine
{
    public static class WebCrmMonitoringService
    {
        private static readonly ILog Log = LogManager.GetLogger("ASC.Mail.WebCrmMonitoringService");
        private static Timer _monitoringTimer;
        private static readonly object _lockObject = new object();
        private static bool _isRunning = false;
        private static DateTime _lastProcessedTime = DateTime.UtcNow.AddMinutes(-5);
        
        public static void Start()
        {
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    Log.Info("WebCrmMonitoringService is already running");
                    return;
                }
                
                Log.Info("Starting WebCrmMonitoringService...");
                
                // Start timer to check for new emails every 30 seconds
                _monitoringTimer = new Timer(ProcessNewEmails, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
                _isRunning = true;
                
                Log.Info("WebCrmMonitoringService started successfully");
            }
        }
        
        public static void Stop()
        {
            lock (_lockObject)
            {
                if (!_isRunning)
                {
                    Log.Info("WebCrmMonitoringService is not running");
                    return;
                }
                
                Log.Info("Stopping WebCrmMonitoringService...");
                
                if (_monitoringTimer != null)
                {
                    _monitoringTimer.Dispose();
                    _monitoringTimer = null;
                }
                
                _isRunning = false;
                Log.Info("WebCrmMonitoringService stopped");
            }
        }
        
        private static void ProcessNewEmails(object state)
        {
            if (!_isRunning) return;
            
            try
            {
                Log.InfoFormat("WebCrmMonitoringService: Checking for new emails since {0:yyyy-MM-dd HH:mm:ss}", _lastProcessedTime);
                
                var currentTime = DateTime.UtcNow;
                var newEmails = GetNewUnlinkedEmails(_lastProcessedTime);
                
                if (newEmails.Count > 0)
                {
                    Log.InfoFormat("Found {0} new unlinked emails to process", newEmails.Count);
                    
                    var processed = 0;
                    var linked = 0;
                    var errors = 0;
                    
                    foreach (var email in newEmails)
                    {
                        try
                        {
                            processed++;
                            
                            if (ProcessEmailForCrmAutoLinking(email))
                            {
                                linked++;
                                Log.InfoFormat("Successfully auto-linked email {0} to CRM", email.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            Log.ErrorFormat("Error processing email {0}: {1}", email.Id, ex.Message);
                        }
                    }
                    
                    Log.InfoFormat("WebCrmMonitoringService: Processed {0} emails, linked {1}, errors {2}", processed, linked, errors);
                }
                else
                {
                    Log.Debug("No new unlinked emails found");
                }
                
                _lastProcessedTime = currentTime;
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("WebCrmMonitoringService error: {0}", ex.Message);
            }
        }
        
        private static List<MailMessageData> GetNewUnlinkedEmails(DateTime since)
        {
            var emails = new List<MailMessageData>();
            
            try
            {
                var connectionString = GetConnectionString();
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    
                    // Get emails that are not linked to CRM and were received recently
                    var query = @"
                        SELECT DISTINCT m.id, m.tenant, m.id_user, m.id_mailbox, m.chain_id,
                               m.from_text, m.to_text, m.cc, m.date_received, m.folder
                        FROM mail_mail m
                        LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
                        WHERE m.date_received >= @since
                        AND m.folder IN (1, 2)  -- Inbox and Sent
                        AND l.id_chain IS NULL  -- Not already linked
                        AND m.is_removed = 0
                        ORDER BY m.date_received DESC
                        LIMIT 50";
                    
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@since", since);
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var email = new MailMessageData
                                {
                                    Id = reader.GetInt32("id"),
                                    TenantId = reader.GetInt32("tenant"),
                                    UserId = reader.GetString("id_user"),
                                    MailboxId = reader.GetInt32("id_mailbox"),
                                    ChainId = reader.GetString("chain_id"),
                                    From = reader.GetString("from_text"),
                                    To = reader.GetString("to_text"),
                                    Cc = reader.IsDBNull("cc") ? "" : reader.GetString("cc"),
                                    DateReceived = reader.GetDateTime("date_received"),
                                    Folder = reader.GetInt32("folder")
                                };
                                
                                emails.Add(email);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error getting new unlinked emails: {0}", ex.Message);
            }
            
            return emails;
        }
        
        private static bool ProcessEmailForCrmAutoLinking(MailMessageData message)
        {
            try
            {
                // Set OnlyOffice context for this tenant/user
                CoreContext.TenantManager.SetCurrentTenant(message.TenantId);
                SecurityContext.AuthenticateMe(new Guid(message.UserId));
                
                // Extract all email addresses from the message
                var allEmails = new List<string>();
                allEmails.AddRange(MailAddressHelper.ParseAddresses(message.From));
                allEmails.AddRange(MailAddressHelper.ParseAddresses(message.To));
                if (!string.IsNullOrEmpty(message.Cc))
                    allEmails.AddRange(MailAddressHelper.ParseAddresses(message.Cc));
                
                // Find matching CRM contacts
                var contactsToLink = new List<CrmContactData>();
                
                using (var daoFactory = new ASC.Mail.Core.Dao.DaoFactory())
                {
                    var crmContactDao = daoFactory.CreateCrmContactDao(message.TenantId, message.UserId);
                    
                    foreach (var email in allEmails.Distinct().Where(e => !string.IsNullOrEmpty(e)))
                    {
                        try
                        {
                            var existingContactIds = crmContactDao.GetCrmContactIds(email);
                            foreach (var contactId in existingContactIds)
                            {
                                if (!contactsToLink.Any(c => c.Id == contactId && c.Type == CrmContactData.EntityTypes.Contact))
                                {
                                    contactsToLink.Add(new CrmContactData
                                    {
                                        Id = contactId,
                                        Type = CrmContactData.EntityTypes.Contact
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.WarnFormat("Error finding CRM contacts for email {0}: {1}", email, ex.Message);
                        }
                    }
                }
                
                if (contactsToLink.Count == 0)
                {
                    Log.DebugFormat("No CRM contacts found for email {0}", message.Id);
                    return false;
                }
                
                // Create CRM link engine and perform enhanced linking
                var crmEngine = new CrmLinkEngine(message.TenantId, message.UserId, Log);
                
                // Check if already linked (double-check)
                var existingLinks = crmEngine.GetLinkedCrmEntitiesId(message.Id);
                if (existingLinks.Count > 0)
                {
                    Log.DebugFormat("Email {0} already linked to {1} CRM entities", message.Id, existingLinks.Count);
                    return false;
                }
                
                // Perform the enhanced CRM linking
                crmEngine.LinkChainToCrmEnhanced(message.Id, contactsToLink, "http");
                
                Log.InfoFormat("Enhanced CRM linking: Successfully linked email {0} to {1} CRM contacts", message.Id, contactsToLink.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error in ProcessEmailForCrmAutoLinking for email {0}: {1}", message.Id, ex.Message);
                return false;
            }
        }
        
        private static string GetConnectionString()
        {
            // Use OnlyOffice's database connection string
            return "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Pooling=true;Character Set=utf8;AutoEnlist=false;SSL Mode=none;AllowPublicKeyRetrieval=true;Connection Timeout=30;Maximum Pool Size=300;";
        }
        
        public static bool IsRunning
        {
            get { lock (_lockObject) { return _isRunning; } }
        }
        
        public static string GetStatus()
        {
            lock (_lockObject)
            {
                return $"WebCrmMonitoringService - Running: {_isRunning}, Last processed: {_lastProcessedTime:yyyy-MM-dd HH:mm:ss}";
            }
        }
    }
    
    /// <summary>
    /// Simple MailMessageData class for email information
    /// </summary>
    public class MailMessageData
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string UserId { get; set; }
        public int MailboxId { get; set; }
        public string ChainId { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public DateTime DateReceived { get; set; }
        public int Folder { get; set; }
    }
}