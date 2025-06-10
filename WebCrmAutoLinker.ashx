<%@ WebHandler Language="C#" Class="WebCrmAutoLinker" %>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.SessionState;
using ASC.Core;
using ASC.Mail.Core.Engine;
using ASC.Mail.Data.Contracts;
using ASC.Mail.Utils;
using ASC.Common.Logging;
using MySql.Data.MySqlClient;

/// <summary>
/// Web CRM Auto Linker
/// HTTP handler that can trigger enhanced CRM linking from web layer
/// Works within OnlyOffice's web application context
/// </summary>
public class WebCrmAutoLinker : IHttpHandler, IRequiresSessionState
{
    private static readonly ILog Log = LogManager.GetLogger("ASC.Mail.WebCrmAutoLinker");
    
    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        
        try
        {
            Log.Info("WebCrmAutoLinker: Processing request");
            
            var action = context.Request.QueryString["action"];
            var emailId = context.Request.QueryString["emailId"];
            var tenantId = context.Request.QueryString["tenantId"];
            var userId = context.Request.QueryString["userId"];
            
            if (action == "link" && !string.IsNullOrEmpty(emailId) && !string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(userId))
            {
                var result = ProcessSingleEmail(int.Parse(emailId), int.Parse(tenantId), userId);
                context.Response.Write(result);
            }
            else if (action == "monitor")
            {
                var result = ProcessRecentEmails();
                context.Response.Write(result);
            }
            else if (action == "test")
            {
                var result = TestEnhancedFunctionality();
                context.Response.Write(result);
            }
            else
            {
                context.Response.Write("{\"success\": false, \"message\": \"Invalid parameters. Use: ?action=link&emailId=123&tenantId=0&userId=guid OR ?action=monitor OR ?action=test\"}");
            }
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("WebCrmAutoLinker error: {0}", ex.Message);
            context.Response.Write("{\"success\": false, \"message\": \"" + ex.Message.Replace("\"", "\\\"") + "\"}");
        }
    }
    
    private string ProcessSingleEmail(int emailId, int tenantId, string userId)
    {
        try
        {
            Log.InfoFormat("Processing email {0} for tenant {1}, user {2}", emailId, tenantId, userId);
            
            // Set OnlyOffice context
            CoreContext.TenantManager.SetCurrentTenant(tenantId);
            SecurityContext.AuthenticateMe(new Guid(userId));
            
            // Create CRM engine
            var crmEngine = new CrmLinkEngine(tenantId, userId, Log);
            
            // Get email details and find CRM contacts
            var contactsToLink = FindMatchingCrmContacts(emailId, tenantId, userId);
            
            if (contactsToLink.Count == 0)
            {
                return "{\"success\": true, \"message\": \"No CRM contacts found for email " + emailId + "\"}";
            }
            
            // Check if already linked
            var existingLinks = crmEngine.GetLinkedCrmEntitiesId(emailId);
            if (existingLinks.Count > 0)
            {
                return "{\"success\": true, \"message\": \"Email " + emailId + " already linked to " + existingLinks.Count + " CRM entities\"}";
            }
            
            // Trigger enhanced CRM linking
            crmEngine.LinkChainToCrmEnhanced(emailId, contactsToLink, HttpContext.Current.Request.Url.Scheme);
            
            return "{\"success\": true, \"message\": \"Successfully linked email " + emailId + " to " + contactsToLink.Count + " CRM contacts using enhanced logic\"}";
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("Error processing email {0}: {1}", emailId, ex.Message);
            return "{\"success\": false, \"message\": \"Error: " + ex.Message.Replace("\"", "\\\"") + "\"}";
        }
    }
    
    private string ProcessRecentEmails()
    {
        try
        {
            Log.Info("Processing recent emails for CRM auto-linking");
            
            var processed = 0;
            var linked = 0;
            var errors = 0;
            
            // Get recent unlinked emails from database
            var connectionString = GetConnectionString();
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                
                var query = @"
                    SELECT DISTINCT m.id, m.tenant, m.id_user, m.from_text, m.to_text, m.cc 
                    FROM mail_mail m
                    LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
                    WHERE m.date_received >= DATE_SUB(NOW(), INTERVAL 1 HOUR)
                    AND m.folder IN (1, 2)
                    AND l.id_chain IS NULL
                    ORDER BY m.date_received DESC
                    LIMIT 50";
                    
                using (var cmd = new MySqlCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    var emails = new List<dynamic>();
                    while (reader.Read())
                    {
                        emails.Add(new {
                            id = reader.GetInt32("id"),
                            tenant = reader.GetInt32("tenant"), 
                            userId = reader.GetString("id_user"),
                            from = reader.GetString("from_text"),
                            to = reader.GetString("to_text"),
                            cc = reader.IsDBNull("cc") ? "" : reader.GetString("cc")
                        });
                    }
                    
                    reader.Close();
                    
                    foreach (var email in emails)
                    {
                        try
                        {
                            processed++;
                            
                            // Set context for each email
                            CoreContext.TenantManager.SetCurrentTenant(email.tenant);
                            SecurityContext.AuthenticateMe(new Guid(email.userId));
                            
                            var contactsToLink = FindMatchingCrmContacts(email.id, email.tenant, email.userId);
                            
                            if (contactsToLink.Count > 0)
                            {
                                var crmEngine = new CrmLinkEngine(email.tenant, email.userId, Log);
                                crmEngine.LinkChainToCrmEnhanced(email.id, contactsToLink, "http");
                                linked++;
                                Log.InfoFormat("Linked email {0} to {1} CRM contacts", email.id, contactsToLink.Count);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            Log.ErrorFormat("Error processing email {0}: {1}", email.id, ex.Message);
                        }
                    }
                }
            }
            
            return "{\"success\": true, \"message\": \"Processed " + processed + " emails, linked " + linked + ", errors " + errors + "\"}";
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("Error in ProcessRecentEmails: {0}", ex.Message);
            return "{\"success\": false, \"message\": \"Error: " + ex.Message.Replace("\"", "\\\"") + "\"}";
        }
    }
    
    private string TestEnhancedFunctionality()
    {
        try
        {
            // Test if enhanced CRM functionality is accessible
            var crmEngine = new CrmLinkEngine(0, SecurityContext.CurrentAccount.ID.ToString(), Log);
            
            return "{\"success\": true, \"message\": \"Enhanced CRM functionality is accessible. CrmLinkEngine created successfully.\"}";
        }
        catch (Exception ex)
        {
            return "{\"success\": false, \"message\": \"Enhanced functionality test failed: " + ex.Message.Replace("\"", "\\\"") + "\"}";
        }
    }
    
    private List<CrmContactData> FindMatchingCrmContacts(int emailId, int tenantId, string userId)
    {
        var contacts = new List<CrmContactData>();
        
        try
        {
            using (var daoFactory = new ASC.Mail.Core.Dao.DaoFactory())
            {
                var mailDao = daoFactory.CreateMailDao(tenantId, userId);
                var message = mailDao.GetMail(new ASC.Mail.Core.Dao.Expressions.Message.ConcreteUserMessageExp(emailId, tenantId, userId));
                
                if (message != null)
                {
                    // Extract email addresses
                    var emails = new List<string>();
                    emails.AddRange(MailAddressHelper.ParseAddresses(message.From));
                    emails.AddRange(MailAddressHelper.ParseAddresses(message.To));
                    if (!string.IsNullOrEmpty(message.Cc))
                        emails.AddRange(MailAddressHelper.ParseAddresses(message.Cc));
                    
                    // Find matching CRM contacts
                    var crmContactDao = daoFactory.CreateCrmContactDao(tenantId, userId);
                    
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
                            Log.WarnFormat("Error finding contacts for email {0}: {1}", email, ex.Message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("Error in FindMatchingCrmContacts for email {0}: {1}", emailId, ex.Message);
        }
        
        return contacts;
    }
    
    private string GetConnectionString()
    {
        // Use OnlyOffice's connection string
        return "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Pooling=true;Character Set=utf8;AutoEnlist=false;SSL Mode=none;AllowPublicKeyRetrieval=true;Connection Timeout=30;Maximum Pool Size=300;";
    }
    
    public bool IsReusable
    {
        get { return false; }
    }
}