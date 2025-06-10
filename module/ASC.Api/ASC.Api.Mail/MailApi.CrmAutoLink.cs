/*
 * Mail API CRM Auto-Link Extension
 * Adds enhanced CRM auto-linking functionality to the existing Mail API
 */

using System;
using System.Collections.Generic;
using System.Linq;
using ASC.Api.Attributes;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Mail.Core.Engine;
using ASC.Mail.Data.Contracts;
using ASC.Mail.Utils;
using MySql.Data.MySqlClient;

namespace ASC.Api.Mail
{
    public partial class MailApi
    {
        private static readonly ILog CrmLog = LogManager.GetLogger("ASC.Mail.CrmAutoLink");

        /// <summary>
        /// Triggers enhanced CRM auto-linking for a specific email
        /// </summary>
        /// <param name="messageId">Email message ID</param>
        /// <returns>CRM linking result</returns>
        [ApiMethod("POST", "api/2.0/mail/crm/autolink/{messageId}", true)]
        public object LinkEmailToCrmEnhanced(int messageId)
        {
            try
            {
                CrmLog.InfoFormat("API: LinkEmailToCrmEnhanced called for message {0}", messageId);

                var result = ProcessEmailForEnhancedCrmLinking(messageId);
                
                return new
                {
                    success = result.Success,
                    message = result.Message,
                    messageId = messageId,
                    contactsLinked = result.ContactsLinked,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                CrmLog.ErrorFormat("API error in LinkEmailToCrmEnhanced: {0}", ex.Message);
                return new
                {
                    success = false,
                    message = ex.Message,
                    messageId = messageId,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Processes recent emails for CRM auto-linking
        /// </summary>
        /// <param name="hours">Hours to look back (default: 1)</param>
        /// <returns>Processing results</returns>
        [ApiMethod("POST", "api/2.0/mail/crm/autolink/recent", true)]
        public object ProcessRecentEmailsForCrm(int hours = 1)
        {
            try
            {
                CrmLog.InfoFormat("API: ProcessRecentEmailsForCrm called for last {0} hours", hours);

                var processed = 0;
                var linked = 0;
                var errors = 0;
                var results = new List<object>();

                var recentEmails = GetRecentUnlinkedEmails(hours);
                
                foreach (var email in recentEmails)
                {
                    try
                    {
                        processed++;
                        var result = ProcessEmailForEnhancedCrmLinking(email.Id);
                        
                        if (result.Success && result.ContactsLinked > 0)
                        {
                            linked++;
                            results.Add(new
                            {
                                emailId = email.Id,
                                success = true,
                                contactsLinked = result.ContactsLinked,
                                message = result.Message
                            });
                        }
                        else if (!result.Success)
                        {
                            errors++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        CrmLog.ErrorFormat("Error processing email {0}: {1}", email.Id, ex.Message);
                    }
                }

                return new
                {
                    success = true,
                    processed = processed,
                    linked = linked,
                    errors = errors,
                    results = results,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                CrmLog.ErrorFormat("API error in ProcessRecentEmailsForCrm: {0}", ex.Message);
                return new
                {
                    success = false,
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Gets the status of the CRM monitoring service
        /// </summary>
        /// <returns>Service status</returns>
        [ApiMethod("GET", "api/2.0/mail/crm/autolink/status", true)]
        public object GetCrmAutoLinkStatus()
        {
            try
            {
                // Check if WebCrmMonitoringService is running
                var serviceStatus = GetMonitoringServiceStatus();
                
                return new
                {
                    success = true,
                    serviceRunning = serviceStatus.IsRunning,
                    lastProcessed = serviceStatus.LastProcessed,
                    status = serviceStatus.Status,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                CrmLog.ErrorFormat("API error in GetCrmAutoLinkStatus: {0}", ex.Message);
                return new
                {
                    success = false,
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Tests the enhanced CRM functionality
        /// </summary>
        /// <param name="emailIds">Test email IDs (e.g., "5006,5007,5008,5009")</param>
        /// <returns>Test results</returns>
        [ApiMethod("POST", "api/2.0/mail/crm/autolink/test", true)]
        public object TestEnhancedCrmFunctionality(string emailIds = "5006,5007,5008,5009")
        {
            try
            {
                CrmLog.InfoFormat("API: TestEnhancedCrmFunctionality called with emails: {0}", emailIds);

                var testResults = new List<object>();
                var emailIdList = emailIds.Split(',').Select(id => int.Parse(id.Trim())).ToList();

                foreach (var emailId in emailIdList)
                {
                    try
                    {
                        var result = ProcessEmailForEnhancedCrmLinking(emailId);
                        testResults.Add(new
                        {
                            emailId = emailId,
                            success = result.Success,
                            message = result.Message,
                            contactsLinked = result.ContactsLinked
                        });
                    }
                    catch (Exception ex)
                    {
                        testResults.Add(new
                        {
                            emailId = emailId,
                            success = false,
                            message = ex.Message,
                            contactsLinked = 0
                        });
                    }
                }

                return new
                {
                    success = true,
                    testResults = testResults,
                    totalTested = emailIdList.Count,
                    successfulLinks = testResults.Count(r => (bool)((dynamic)r).success && (int)((dynamic)r).contactsLinked > 0),
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                CrmLog.ErrorFormat("API error in TestEnhancedCrmFunctionality: {0}", ex.Message);
                return new
                {
                    success = false,
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        private CrmLinkResult ProcessEmailForEnhancedCrmLinking(int messageId)
        {
            try
            {
                CrmLog.InfoFormat("Processing email {0} for enhanced CRM linking", messageId);

                // Create CRM engine using current context
                var crmEngine = new CrmLinkEngine(TenantId, UserId, CrmLog);
                
                // Get email details and find matching CRM contacts
                var contactsToLink = FindMatchingCrmContactsForEmail(messageId);
                
                if (contactsToLink.Count == 0)
                {
                    return new CrmLinkResult 
                    { 
                        Success = true, 
                        Message = $"No CRM contacts found for email {messageId}",
                        ContactsLinked = 0 
                    };
                }
                
                // Check if already linked
                var existingLinks = crmEngine.GetLinkedCrmEntitiesId(messageId);
                if (existingLinks.Count > 0)
                {
                    return new CrmLinkResult 
                    { 
                        Success = true, 
                        Message = $"Email {messageId} already linked to {existingLinks.Count} CRM entities",
                        ContactsLinked = existingLinks.Count 
                    };
                }
                
                // Trigger enhanced CRM linking - THIS IS THE KEY CALL!
                crmEngine.LinkChainToCrmEnhanced(messageId, contactsToLink, "http");
                
                CrmLog.InfoFormat("✅ Successfully used LinkChainToCrmEnhanced for email {0} with {1} contacts", messageId, contactsToLink.Count);
                
                return new CrmLinkResult 
                { 
                    Success = true, 
                    Message = $"✅ Successfully linked email {messageId} to {contactsToLink.Count} CRM contacts using enhanced logic",
                    ContactsLinked = contactsToLink.Count 
                };
            }
            catch (Exception ex)
            {
                CrmLog.ErrorFormat("Error in ProcessEmailForEnhancedCrmLinking for email {0}: {1}", messageId, ex.Message);
                return new CrmLinkResult 
                { 
                    Success = false, 
                    Message = $"Error: {ex.Message}",
                    ContactsLinked = 0 
                };
            }
        }

        private List<CrmContactData> FindMatchingCrmContactsForEmail(int messageId)
        {
            var contacts = new List<CrmContactData>();
            
            try
            {
                var message = EngineFactory.MessageEngine.GetMessage(messageId, new MailMessageData.Options
                {
                    LoadImages = false,
                    LoadBody = false,
                    NeedProxyHttp = false
                });
                
                if (message != null)
                {
                    // Extract email addresses
                    var emails = new List<string>();
                    emails.AddRange(MailAddressHelper.ParseAddresses(message.From));
                    emails.AddRange(MailAddressHelper.ParseAddresses(message.To));
                    if (!string.IsNullOrEmpty(message.Cc))
                        emails.AddRange(MailAddressHelper.ParseAddresses(message.Cc));
                    
                    // Find matching CRM contacts
                    using (var daoFactory = new ASC.Mail.Core.Dao.DaoFactory())
                    {
                        var crmContactDao = daoFactory.CreateCrmContactDao(TenantId, UserId);
                        
                        foreach (var email in emails.Distinct().Where(e => !string.IsNullOrEmpty(e)))
                        {
                            try
                            {
                                var contactIds = crmContactDao.GetCrmContactIds(email);
                                foreach (var contactId in contactIds)
                                {
                                    if (!contacts.Any(c => c.Id == contactId && c.Type == CrmContactData.EntityTypes.Contact))
                                    {
                                        contacts.Add(new CrmContactData
                                        {
                                            Id = contactId,
                                            Type = CrmContactData.EntityTypes.Contact
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                CrmLog.WarnFormat("Error finding contacts for email {0}: {1}", email, ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CrmLog.ErrorFormat("Error in FindMatchingCrmContactsForEmail for message {0}: {1}", messageId, ex.Message);
            }
            
            return contacts;
        }

        private List<dynamic> GetRecentUnlinkedEmails(int hours)
        {
            var emails = new List<dynamic>();
            
            try
            {
                var connectionString = GetConnectionString();
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    
                    var query = @"
                        SELECT DISTINCT m.id, m.tenant, m.id_user
                        FROM mail_mail m
                        LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
                        WHERE m.date_received >= DATE_SUB(NOW(), INTERVAL @hours HOUR)
                        AND m.folder IN (1, 2)
                        AND l.id_chain IS NULL
                        AND m.is_removed = 0
                        AND m.tenant = @tenant
                        ORDER BY m.date_received DESC
                        LIMIT 50";
                    
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@hours", hours);
                        cmd.Parameters.AddWithValue("@tenant", TenantId);
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                emails.Add(new
                                {
                                    Id = reader.GetInt32("id"),
                                    TenantId = reader.GetInt32("tenant"),
                                    UserId = reader.GetString("id_user")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CrmLog.ErrorFormat("Error getting recent unlinked emails: {0}", ex.Message);
            }
            
            return emails;
        }

        private dynamic GetMonitoringServiceStatus()
        {
            try
            {
                // Try to get status from WebCrmMonitoringService via reflection
                var mailAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "ASC.Mail");
                
                if (mailAssembly != null)
                {
                    var serviceType = mailAssembly.GetType("ASC.Mail.Core.Engine.WebCrmMonitoringService");
                    if (serviceType != null)
                    {
                        var isRunningProperty = serviceType.GetProperty("IsRunning", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var getStatusMethod = serviceType.GetMethod("GetStatus", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        
                        if (isRunningProperty != null && getStatusMethod != null)
                        {
                            var isRunning = (bool)isRunningProperty.GetValue(null);
                            var status = getStatusMethod.Invoke(null, null);
                            
                            return new
                            {
                                IsRunning = isRunning,
                                Status = status.ToString(),
                                LastProcessed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                            };
                        }
                    }
                }
                
                return new
                {
                    IsRunning = false,
                    Status = "Service not found",
                    LastProcessed = "Unknown"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    IsRunning = false,
                    Status = $"Error: {ex.Message}",
                    LastProcessed = "Unknown"
                };
            }
        }

        private string GetConnectionString()
        {
            return "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Pooling=true;Character Set=utf8;AutoEnlist=false;SSL Mode=none;AllowPublicKeyRetrieval=true;Connection Timeout=30;Maximum Pool Size=300;";
        }
    }

    /// <summary>
    /// Result of CRM linking operation
    /// </summary>
    public class CrmLinkResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ContactsLinked { get; set; }
    }
}