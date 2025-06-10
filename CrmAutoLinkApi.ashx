<%@ WebHandler Language="C#" Class="CrmAutoLinkApi" %>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ASC.Core;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Dao;
using ASC.Mail.Data.Contracts;
using ASC.Mail.Utils;

/// <summary>
/// CRM Auto-Link API Endpoint
/// Receives email ID and triggers CRM auto-linking using existing OnlyOffice infrastructure
/// </summary>
public class CrmAutoLinkApi : IHttpHandler 
{
    public void ProcessRequest(HttpContext context) 
    {
        context.Response.ContentType = "text/plain";
        
        try
        {
            // Get parameters
            var emailIdStr = context.Request.QueryString["emailId"];
            var tenantIdStr = context.Request.QueryString["tenantId"];
            var userIdStr = context.Request.QueryString["userId"];
            
            if (string.IsNullOrEmpty(emailIdStr) || string.IsNullOrEmpty(tenantIdStr) || string.IsNullOrEmpty(userIdStr))
            {
                context.Response.Write("ERROR: Missing required parameters (emailId, tenantId, userId)");
                return;
            }
            
            int emailId, tenantId;
            if (!int.TryParse(emailIdStr, out emailId) || !int.TryParse(tenantIdStr, out tenantId))
            {
                context.Response.Write("ERROR: Invalid emailId or tenantId format");
                return;
            }
            
            string userId = userIdStr;
            
            // Log the request
            string logMsg = $"[{DateTime.Now:HH:mm:ss}] CrmAutoLinkApi: Processing email {emailId} for tenant {tenantId}, user {userId}";
            System.Diagnostics.Debug.WriteLine(logMsg);
            
            // Set OnlyOffice context
            CoreContext.TenantManager.SetCurrentTenant(tenantId);
            SecurityContext.AuthenticateMe(new Guid(userId));
            
            // Process the email
            var result = ProcessEmailForCrmLinking(emailId, tenantId, userId);
            
            if (result.Success)
            {
                context.Response.Write($"SUCCESS: {result.Message}");
            }
            else
            {
                context.Response.Write($"ERROR: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"EXCEPTION: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"CrmAutoLinkApi Error: {errorMsg}");
            context.Response.Write(errorMsg);
        }
    }
    
    private ProcessResult ProcessEmailForCrmLinking(int emailId, int tenantId, string userId)
    {
        try
        {
            // Create engine factory with proper context
            var engineFactory = new EngineFactory(tenantId, userId);
            
            // Load the email message
            var message = engineFactory.MessageEngine.GetMessage(emailId, new MailMessageData.Options
            {
                LoadImages = false,
                LoadBody = false,
                NeedProxyHttp = false
            });
            
            if (message == null)
            {
                return new ProcessResult { Success = false, Message = $"Email {emailId} not found" };
            }
            
            // Extract all email addresses
            var allEmails = new List<string>();
            allEmails.AddRange(MailAddressHelper.ParseAddresses(message.From));
            allEmails.AddRange(MailAddressHelper.ParseAddresses(message.To));
            allEmails.AddRange(MailAddressHelper.ParseAddresses(message.Cc));
            
            // Find matching CRM contacts
            var contactsToLink = new List<CrmContactData>();
            
            using (var daoFactory = new DaoFactory())
            {
                var crmContactDao = daoFactory.CreateCrmContactDao(tenantId, userId);
                
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
                        System.Diagnostics.Debug.WriteLine($"CrmAutoLinkApi: Error finding contacts for {email}: {ex.Message}");
                    }
                }
            }
            
            if (!contactsToLink.Any())
            {
                return new ProcessResult { Success = true, Message = $"No CRM contacts found for email {emailId}" };
            }
            
            // Create CRM link engine and perform enhanced linking
            var crmEngine = new CrmLinkEngine(tenantId, userId);
            
            // Check if already linked
            var existingLinks = crmEngine.GetLinkedCrmEntitiesId(emailId);
            if (existingLinks.Any())
            {
                return new ProcessResult { Success = true, Message = $"Email {emailId} already linked to {existingLinks.Count} CRM entities" };
            }
            
            // Perform the enhanced CRM linking
            crmEngine.LinkChainToCrmEnhanced(emailId, contactsToLink, HttpContext.Current?.Request?.Url?.Scheme ?? "http");
            
            return new ProcessResult { 
                Success = true, 
                Message = $"✅ Successfully linked email {emailId} to {contactsToLink.Count} CRM contacts" 
            };
        }
        catch (Exception ex)
        {
            return new ProcessResult { 
                Success = false, 
                Message = $"Error processing email {emailId}: {ex.Message}" 
            };
        }
    }
    
    public bool IsReusable 
    {
        get { return false; }
    }
}

public class ProcessResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
}