/*
 * CRM Email Auto-Link Service
 * Automatically links incoming and outgoing emails to CRM contacts
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

using ASC.Common.Data;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Tenants;
using ASC.Mail.Core.Dao;
using ASC.Mail.Core.Dao.Expressions.Message;
using ASC.Mail.Data.Contracts;
using ASC.Mail.Utils;
using ASC.CRM.Core.Entities;

namespace ASC.Mail.Core.Engine
{
    /// <summary>
    /// Service that monitors for new emails and automatically links them to CRM contacts
    /// </summary>
    public static class CrmEmailAutoLinkService
    {
        private static readonly ILog Log = LogManager.GetLogger("ASC.CrmEmailAutoLinkService");
        private static Timer _monitoringTimer;
        private static DateTime _lastProcessedTime = DateTime.UtcNow.AddMinutes(-5);
        private static bool _isRunning = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Start the auto-linking service
        /// </summary>
        public static void Start()
        {
            lock (_lockObject)
            {
                if (_monitoringTimer != null) return;

                Log.Info("🚀 CrmEmailAutoLinkService: Starting CRM email auto-linking service...");

                // Start monitoring every 30 seconds
                _monitoringTimer = new Timer(ProcessNewEmails, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

                Log.Info("✅ CrmEmailAutoLinkService: Auto-linking service started successfully");
            }
        }

        /// <summary>
        /// Stop the auto-linking service
        /// </summary>
        public static void Stop()
        {
            lock (_lockObject)
            {
                if (_monitoringTimer == null) return;

                Log.Info("🛑 CrmEmailAutoLinkService: Stopping CRM email auto-linking service...");

                _monitoringTimer.Dispose();
                _monitoringTimer = null;

                Log.Info("✅ CrmEmailAutoLinkService: Auto-linking service stopped successfully");
            }
        }

        /// <summary>
        /// Process new emails for CRM auto-linking
        /// </summary>
        private static void ProcessNewEmails(object state)
        {
            if (_isRunning) return;

            _isRunning = true;
            try
            {
                Log.Debug($"🔍 CrmEmailAutoLinkService: Checking for new emails since {_lastProcessedTime:yyyy-MM-dd HH:mm:ss}");

                var processedCount = 0;
                var tenants = CoreContext.TenantManager.GetTenants().Where(t => t.Status == TenantStatus.Active);

                foreach (var tenant in tenants)
                {
                    try
                    {
                        processedCount += ProcessTenantEmails(tenant.TenantId);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"❌ CrmEmailAutoLinkService: Error processing emails for tenant {tenant.TenantId}: {ex.Message}", ex);
                    }
                }

                if (processedCount > 0)
                {
                    Log.Info($"✅ CrmEmailAutoLinkService: Processed {processedCount} emails for CRM auto-linking");
                }

                _lastProcessedTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Log.Error("❌ CrmEmailAutoLinkService: Error in monitoring cycle", ex);
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Process emails for a specific tenant
        /// </summary>
        private static int ProcessTenantEmails(int tenantId)
        {
            var processedCount = 0;

            try
            {
                CoreContext.TenantManager.SetCurrentTenant(tenantId);

                // Use database queries to find recent emails for CRM auto-linking
                using (var db = DbManager.FromHttpContext(tenantId.ToString()))
                {
                    // Get unprocessed emails from the last period using direct SQL
                    var query = @"
                        SELECT m.id, m.id_user, m.from_text, m.to_text, m.cc 
                        FROM mail_mail m
                        LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
                        WHERE m.tenant = @tenant 
                        AND m.date_received >= @lastProcessed 
                        AND m.date_received <= @now
                        AND m.folder IN (1, 2)
                        AND l.id_chain IS NULL
                        ORDER BY m.date_received DESC
                        LIMIT 100";

                    var unprocessedEmails = db.ExecuteList(query, 
                        new { tenant = tenantId, lastProcessed = _lastProcessedTime, now = DateTime.UtcNow })
                        .Select(r => new 
                        {
                            Id = Convert.ToInt32(r[0]),
                            UserId = r[1].ToString(),
                            From = r[2].ToString(),
                            To = r[3].ToString(),
                            Cc = r[4]?.ToString()
                        }).ToList();

                    if (!unprocessedEmails.Any()) return 0;

                    Log.Info($"📧 CrmEmailAutoLinkService: Found {unprocessedEmails.Count} unprocessed emails for tenant {tenantId}");

                    foreach (var mailInfo in unprocessedEmails)
                    {
                        try
                        {
                            // Set security context for the email owner
                            SecurityContext.AuthenticateMe(new Guid(mailInfo.UserId));

                            var engine = new EngineFactory(tenantId, mailInfo.UserId);
                            var message = engine.MessageEngine.GetMessage(mailInfo.Id, new MailMessageData.Options
                            {
                                LoadImages = false,
                                LoadBody = false,
                                NeedProxyHttp = false
                            });

                            if (ProcessEmailForCrmAutoLinking(message, tenantId, mailInfo.UserId))
                            {
                                processedCount++;
                                Log.Info($"🎯 CrmEmailAutoLinkService: Auto-linked email {mailInfo.Id} to CRM for tenant {tenantId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warn($"⚠️ CrmEmailAutoLinkService: Error processing email {mailInfo.Id} for tenant {tenantId}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"❌ CrmEmailAutoLinkService: Error processing tenant {tenantId}: {ex.Message}", ex);
            }

            return processedCount;
        }

        /// <summary>
        /// Process a single email for CRM auto-linking using the enhanced logic
        /// </summary>
        private static bool ProcessEmailForCrmAutoLinking(MailMessageData message, int tenantId, string userId)
        {
            try
            {
                Log.Debug($"🔧 CrmEmailAutoLinkService: Processing email {message.Id} from {message.From} for CRM auto-linking");

                // Create CRM link engine
                var crmEngine = new CrmLinkEngine(tenantId, userId, Log);

                // Check if already linked
                var existingLinks = crmEngine.GetLinkedCrmEntitiesId(message.Id);
                if (existingLinks.Any())
                {
                    Log.Debug($"📭 CrmEmailAutoLinkService: Email {message.Id} already linked to {existingLinks.Count} CRM entities");
                    return false;
                }

                // Extract email addresses
                var allEmails = new List<string>();
                allEmails.AddRange(MailAddressHelper.ParseAddresses(message.From));
                allEmails.AddRange(MailAddressHelper.ParseAddresses(message.To));
                allEmails.AddRange(MailAddressHelper.ParseAddresses(message.Cc));

                var contactsToLink = new List<CrmContactData>();

                using (var daoFactory = new DaoFactory())
                {
                    var crmContactDao = daoFactory.CreateCrmContactDao(tenantId, userId);

                    foreach (var email in allEmails.Distinct().Where(e => !string.IsNullOrEmpty(e)))
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
                }

                // Link using the enhanced method if contacts found
                if (contactsToLink.Any())
                {
                    Log.Info($"🎯 CrmEmailAutoLinkService: Found {contactsToLink.Count} CRM contacts for email {message.Id}");

                    // Use the full enhanced linking process that creates relationship events and shows in interface
                    crmEngine.LinkChainToCrmEnhanced(message.Id, contactsToLink, HttpContext.Current?.Request?.Url?.Scheme ?? "http");

                    Log.Info($"✅ CrmEmailAutoLinkService: Successfully auto-linked email {message.Id} to {contactsToLink.Count} CRM contacts");
                    return true;
                }
                else
                {
                    Log.Debug($"📭 CrmEmailAutoLinkService: No CRM contacts found for email {message.Id}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"⚠️ CrmEmailAutoLinkService: Error auto-linking email {message.Id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if email is already linked to CRM
        /// </summary>
        private static bool IsEmailAlreadyLinkedToCrm(int tenantId, int messageId)
        {
            try
            {
                using (var daoFactory = new DaoFactory())
                {
                    var daoCrmLink = daoFactory.CreateCrmLinkDao(tenantId, null);
                    var daoMail = daoFactory.CreateMailDao(tenantId, null);

                    var mail = daoMail.GetMail(new ConcreteUserMessageExp(messageId, tenantId, null));
                    if (mail == null) return false;

                    var linkedEntities = daoCrmLink.GetLinkedCrmContactEntities(mail.ChainId, mail.MailboxId);
                    return linkedEntities.Any();
                }
            }
            catch
            {
                return false; // Assume not linked if we can't check
            }
        }
    }
}