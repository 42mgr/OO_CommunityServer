using System;
using System.Collections.Generic;
using System.Threading;
using System.Data;
using ASC.Core;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Data.Contracts;
using ASC.Common.Data;
using ASC.Common.Logging;
using log4net;

namespace ASC.Mail.Enhanced
{
    /// <summary>
    /// Simple background CRM auto-linker that monitors for new emails
    /// </summary>
    public class SimpleCrmAutoLinker
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SimpleCrmAutoLinker));
        private static Timer _processingTimer;
        private static DateTime _lastProcessedTime = DateTime.UtcNow.AddMinutes(-5);
        private static bool _isProcessing = false;
        
        /// <summary>
        /// Initialize the auto-linker background service
        /// </summary>
        public static void Initialize()
        {
            Log.Info("🚀 SimpleCrmAutoLinker: Initializing enhanced CRM auto-linking service...");
            
            try
            {
                // Start timer to check for new emails every 30 seconds
                _processingTimer = new Timer(ProcessNewEmails, null, 
                                           TimeSpan.FromSeconds(30), 
                                           TimeSpan.FromSeconds(30));
                
                Log.Info("✅ SimpleCrmAutoLinker: Enhanced CRM auto-linking service started successfully");
            }
            catch (Exception ex)
            {
                Log.Error("❌ SimpleCrmAutoLinker: Failed to initialize", ex);
            }
        }
        
        private static void ProcessNewEmails(object state)
        {
            if (_isProcessing) return; // Prevent overlapping executions
            
            _isProcessing = true;
            
            try
            {
                Log.Debug("🔍 SimpleCrmAutoLinker: Checking for new emails to process...");
                
                // Get all tenants
                var tenants = CoreContext.TenantManager.GetTenants();
                
                foreach (var tenant in tenants)
                {
                    try
                    {
                        ProcessTenantEmails(tenant.TenantId);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorFormat("❌ SimpleCrmAutoLinker: Error processing emails for tenant {0}: {1}", 
                                       tenant.TenantId, ex.Message);
                    }
                }
                
                _lastProcessedTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Log.Error("❌ SimpleCrmAutoLinker: Error in ProcessNewEmails", ex);
            }
            finally
            {
                _isProcessing = false;
            }
        }
        
        private static void ProcessTenantEmails(int tenantId)
        {
            try
            {
                CoreContext.TenantManager.SetCurrentTenant(tenantId);
                
                // Query for new inbox emails without CRM links
                var newEmails = GetUnprocessedInboxEmails(tenantId);
                
                if (newEmails.Count > 0)
                {
                    Log.InfoFormat("🔍 SimpleCrmAutoLinker: Found {0} new emails to process for tenant {1}", 
                                  newEmails.Count, tenantId);
                    
                    foreach (var email in newEmails)
                    {
                        try
                        {
                            ProcessEmailForEnhancedCrm(email, tenantId);
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorFormat("❌ SimpleCrmAutoLinker: Error processing email {0}: {1}", 
                                           email["id"], ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("❌ SimpleCrmAutoLinker: Error in ProcessTenantEmails for tenant {0}: {1}", 
                               tenantId, ex.Message);
            }
        }
        
        private static List<Dictionary<string, object>> GetUnprocessedInboxEmails(int tenantId)
        {
            var emails = new List<Dictionary<string, object>>();
            
            try
            {
                using (var db = new DbManager("mail"))
                {
                    var query = @"
                        SELECT m.id, m.from_text, m.to_text, m.cc, m.subject, m.date_received, m.id_user, m.id_mailbox
                        FROM mail_mail m
                        WHERE m.tenant = @tenant 
                          AND m.folder = 2  -- Inbox folder
                          AND m.date_received > @lastProcessed
                          AND NOT EXISTS (
                              SELECT 1 FROM crm_relationship_event cre 
                              WHERE cre.entity_type = 0 AND cre.entity_id = m.id
                          )
                        ORDER BY m.date_received ASC
                        LIMIT 20";  // Process max 20 emails per cycle
                    
                    var results = db.ExecuteList(query, new { 
                        tenant = tenantId, 
                        lastProcessed = _lastProcessedTime 
                    });
                    
                    foreach (var result in results)
                    {
                        var email = new Dictionary<string, object>();
                        for (int i = 0; i < result.Length; i++)
                        {
                            var columnName = i switch
                            {
                                0 => "id",
                                1 => "from_text", 
                                2 => "to_text",
                                3 => "cc",
                                4 => "subject",
                                5 => "date_received",
                                6 => "id_user",
                                7 => "id_mailbox",
                                _ => $"col_{i}"
                            };
                            email[columnName] = result[i];
                        }
                        emails.Add(email);
                    }
                    
                    Log.DebugFormat("🔍 SimpleCrmAutoLinker: Found {0} unprocessed emails for tenant {1} since {2}", 
                                   emails.Count, tenantId, _lastProcessedTime);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("❌ SimpleCrmAutoLinker: Error querying unprocessed emails: {0}", ex.Message);
            }
            
            return emails;
        }
        
        private static void ProcessEmailForEnhancedCrm(Dictionary<string, object> emailData, int tenantId)
        {
            try
            {
                var emailId = Convert.ToInt32(emailData["id"]);
                var fromText = emailData["from_text"]?.ToString() ?? "";
                var userId = emailData["id_user"]?.ToString() ?? "";
                var mailboxId = Convert.ToInt32(emailData["id_mailbox"]);
                
                Log.InfoFormat("🔧 SimpleCrmAutoLinker: Processing email {0} - DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED for message {0} from {1}", 
                              emailId, fromText);
                
                // Set security context for the email owner
                CoreContext.TenantManager.SetCurrentTenant(tenantId);
                SecurityContext.AuthenticateMe(new Guid(userId));
                
                // Create message data object
                var message = new MailMessageData
                {
                    Id = emailId,
                    From = fromText,
                    To = emailData["to_text"]?.ToString() ?? "",
                    Cc = emailData["cc"]?.ToString() ?? "",
                    Subject = emailData["subject"]?.ToString() ?? "",
                    Date = Convert.ToDateTime(emailData["date_received"]),
                    UserId = userId,
                    MailboxId = mailboxId
                };
                
                // Create mailbox data
                var mailbox = new MailBoxData
                {
                    TenantId = tenantId,
                    UserId = userId,
                    MailBoxId = mailboxId
                };
                
                // Create CRM link engine with enhanced functionality
                var crmEngine = new CrmLinkEngine(tenantId, userId, Log);
                
                // Apply enhanced CRM processing
                crmEngine.ProcessIncomingEmailForCrm(message, mailbox, "http");
                
                Log.InfoFormat("✅ SimpleCrmAutoLinker: Enhanced CRM auto-processing completed for message {0}", emailId);
            }
            catch (Exception ex)
            {
                Log.WarnFormat("⚠️ SimpleCrmAutoLinker: Enhanced CRM auto-processing failed for message {0}: {1}", 
                              emailData["id"], ex.Message);
            }
        }
        
        /// <summary>
        /// Stop the auto-linker service
        /// </summary>
        public static void Stop()
        {
            try
            {
                _processingTimer?.Dispose();
                Log.Info("🛑 SimpleCrmAutoLinker: Enhanced CRM auto-linking service stopped");
            }
            catch (Exception ex)
            {
                Log.Error("❌ SimpleCrmAutoLinker: Error stopping service", ex);
            }
        }
    }
}