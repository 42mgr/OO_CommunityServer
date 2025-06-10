<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="CrmAutoLinkApi.aspx.cs" Inherits="CrmAutoLinkApi" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="System.Collections.Generic" %>
<%@ Import Namespace="System.Linq" %>
<%@ Import Namespace="System.Web" %>
<%@ Import Namespace="ASC.Core" %>
<%@ Import Namespace="ASC.Mail.Core.Engine" %>
<%@ Import Namespace="ASC.Mail.Core.Dao" %>
<%@ Import Namespace="ASC.Mail.Data.Contracts" %>
<%@ Import Namespace="ASC.Mail.Utils" %>

<script runat="server">
protected void Page_Load(object sender, EventArgs e)
{
    Response.ContentType = "text/plain";
    
    try
    {
        // Get parameters
        var emailIdStr = Request.QueryString["emailId"];
        var tenantIdStr = Request.QueryString["tenantId"];
        var userIdStr = Request.QueryString["userId"];
        
        if (string.IsNullOrEmpty(emailIdStr) || string.IsNullOrEmpty(tenantIdStr) || string.IsNullOrEmpty(userIdStr))
        {
            Response.Write("ERROR: Missing required parameters (emailId, tenantId, userId)");
            return;
        }
        
        int emailId, tenantId;
        if (!int.TryParse(emailIdStr, out emailId) || !int.TryParse(tenantIdStr, out tenantId))
        {
            Response.Write("ERROR: Invalid emailId or tenantId format");
            return;
        }
        
        string userId = userIdStr;
        
        // Log the request
        string logMsg = string.Format("[{0:HH:mm:ss}] CrmAutoLinkApi: Processing email {1} for tenant {2}, user {3}", 
            DateTime.Now, emailId, tenantId, userId);
        System.Diagnostics.Debug.WriteLine(logMsg);
        
        // Set OnlyOffice context
        CoreContext.TenantManager.SetCurrentTenant(tenantId);
        SecurityContext.AuthenticateMe(new Guid(userId));
        
        // Process the email
        var result = ProcessEmailForCrmLinking(emailId, tenantId, userId);
        
        if (result.Success)
        {
            Response.Write("SUCCESS: " + result.Message);
        }
        else
        {
            Response.Write("ERROR: " + result.Message);
        }
    }
    catch (Exception ex)
    {
        var errorMsg = "EXCEPTION: " + ex.Message;
        System.Diagnostics.Debug.WriteLine("CrmAutoLinkApi Error: " + errorMsg);
        Response.Write(errorMsg);
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
            return new ProcessResult { Success = false, Message = string.Format("Email {0} not found", emailId) };
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
                    System.Diagnostics.Debug.WriteLine(string.Format("CrmAutoLinkApi: Error finding contacts for {0}: {1}", email, ex.Message));
                }
            }
        }
        
        if (!contactsToLink.Any())
        {
            return new ProcessResult { Success = true, Message = string.Format("No CRM contacts found for email {0}", emailId) };
        }
        
        // Create CRM link engine and perform enhanced linking
        var crmEngine = new CrmLinkEngine(tenantId, userId);
        
        // Check if already linked
        var existingLinks = crmEngine.GetLinkedCrmEntitiesId(emailId);
        if (existingLinks.Any())
        {
            return new ProcessResult { Success = true, Message = string.Format("Email {0} already linked to {1} CRM entities", emailId, existingLinks.Count) };
        }
        
        // Perform the enhanced CRM linking
        crmEngine.LinkChainToCrmEnhanced(emailId, contactsToLink, HttpContext.Current.Request.Url.Scheme);
        
        return new ProcessResult { 
            Success = true, 
            Message = string.Format("✅ Successfully linked email {0} to {1} CRM contacts", emailId, contactsToLink.Count)
        };
    }
    catch (Exception ex)
    {
        return new ProcessResult { 
            Success = false, 
            Message = string.Format("Error processing email {0}: {1}", emailId, ex.Message)
        };
    }
}

public class ProcessResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
}
</script>