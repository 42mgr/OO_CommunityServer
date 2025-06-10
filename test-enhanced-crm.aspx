<%@ Page Language="C#" AutoEventWireup="true" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="System.Collections.Generic" %>
<%@ Import Namespace="System.Linq" %>
<%@ Import Namespace="ASC.Core" %>
<%@ Import Namespace="ASC.Mail.Core.Engine" %>
<%@ Import Namespace="ASC.Mail.Data.Contracts" %>
<%@ Import Namespace="ASC.Mail.Utils" %>
<%@ Import Namespace="ASC.Common.Logging" %>

<script runat="server">
protected void Page_Load(object sender, EventArgs e)
{
    Response.ContentType = "text/html";
    
    try
    {
        // Get test action
        var action = Request.QueryString["action"] ?? "test";
        var emailIds = Request.QueryString["emails"] ?? "5006,5007,5008,5009";
        
        Response.Write("<html><head><title>Enhanced CRM Test</title></head><body>");
        Response.Write("<h1>🔗 Enhanced CRM Auto-Link Test</h1>");
        Response.Write($"<p><strong>Action:</strong> {action}</p>");
        Response.Write($"<p><strong>Time:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        
        if (action == "test")
        {
            TestEnhancedCrmFunctionality(emailIds);
        }
        else if (action == "monitor")
        {
            TestMonitoringService();
        }
        else if (action == "single" && !string.IsNullOrEmpty(Request.QueryString["id"]))
        {
            TestSingleEmail(int.Parse(Request.QueryString["id"]));
        }
        
        Response.Write("<hr/>");
        Response.Write("<h3>Available Tests:</h3>");
        Response.Write("<ul>");
        Response.Write("<li><a href='?action=test'>Test Enhanced CRM (default emails)</a></li>");
        Response.Write("<li><a href='?action=single&id=5006'>Test Single Email (5006)</a></li>");
        Response.Write("<li><a href='?action=monitor'>Test Monitoring Service</a></li>");
        Response.Write("</ul>");
        Response.Write("</body></html>");
    }
    catch (Exception ex)
    {
        Response.Write($"<h2 style='color:red'>❌ Error: {ex.Message}</h2>");
        Response.Write($"<pre>{ex.StackTrace}</pre>");
        Response.Write("</body></html>");
    }
}

private void TestEnhancedCrmFunctionality(string emailIds)
{
    Response.Write("<h2>🧪 Testing Enhanced CRM Functionality</h2>");
    
    try
    {
        var emailIdList = emailIds.Split(',').Select(id => int.Parse(id.Trim())).ToList();
        var log = LogManager.GetLogger("ASC.Mail.CrmTest");
        
        Response.Write($"<p>Testing emails: {string.Join(", ", emailIdList)}</p>");
        
        foreach (var emailId in emailIdList)
        {
            Response.Write($"<div style='border:1px solid #ccc; padding:10px; margin:10px 0;'>");
            Response.Write($"<h3>📧 Email ID: {emailId}</h3>");
            
            try
            {
                // Set current tenant and user context
                var tenantId = 0; // Default tenant
                var userId = "00000000-0000-0000-0000-000000000000"; // System user
                
                CoreContext.TenantManager.SetCurrentTenant(tenantId);
                SecurityContext.AuthenticateMe(new Guid(userId));
                
                Response.Write($"<p>✅ Context set - Tenant: {tenantId}, User: {userId}</p>");
                
                // Create CRM engine
                var crmEngine = new CrmLinkEngine(tenantId, userId, log);
                Response.Write("<p>✅ CrmLinkEngine created successfully</p>");
                
                // Check if email exists and get details
                using (var daoFactory = new ASC.Mail.Core.Dao.DaoFactory())
                {
                    var mailDao = daoFactory.CreateMailDao(tenantId, userId);
                    var message = mailDao.GetMail(new ASC.Mail.Core.Dao.Expressions.Message.ConcreteUserMessageExp(emailId, tenantId, userId));
                    
                    if (message != null)
                    {
                        Response.Write($"<p>✅ Email found - From: {message.From}, To: {message.To}</p>");
                        
                        // Extract email addresses
                        var emails = new List<string>();
                        emails.AddRange(MailAddressHelper.ParseAddresses(message.From));
                        emails.AddRange(MailAddressHelper.ParseAddresses(message.To));
                        if (!string.IsNullOrEmpty(message.Cc))
                            emails.AddRange(MailAddressHelper.ParseAddresses(message.Cc));
                        
                        Response.Write($"<p>📨 Email addresses found: {string.Join(", ", emails.Distinct())}</p>");
                        
                        // Find matching CRM contacts
                        var contactsToLink = new List<CrmContactData>();
                        var crmContactDao = daoFactory.CreateCrmContactDao(tenantId, userId);
                        
                        foreach (var email in emails.Distinct().Where(e => !string.IsNullOrEmpty(e)))
                        {
                            try
                            {
                                var contactIds = crmContactDao.GetCrmContactIds(email);
                                Response.Write($"<p>🔍 {email}: Found {contactIds.Count} contacts</p>");
                                
                                foreach (var contactId in contactIds)
                                {
                                    if (!contactsToLink.Any(c => c.Id == contactId))
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
                                Response.Write($"<p style='color:orange'>⚠️ Error finding contacts for {email}: {ex.Message}</p>");
                            }
                        }
                        
                        if (contactsToLink.Count > 0)
                        {
                            Response.Write($"<p>🎯 Total CRM contacts to link: {contactsToLink.Count}</p>");
                            
                            // Check if already linked
                            var existingLinks = crmEngine.GetLinkedCrmEntitiesId(emailId);
                            if (existingLinks.Count > 0)
                            {
                                Response.Write($"<p style='color:blue'>ℹ️ Email already linked to {existingLinks.Count} CRM entities</p>");
                            }
                            else
                            {
                                // THIS IS THE KEY MOMENT - Call the enhanced CRM linking!
                                Response.Write("<p style='background:yellow; padding:5px;'><strong>🚀 Calling LinkChainToCrmEnhanced...</strong></p>");
                                
                                crmEngine.LinkChainToCrmEnhanced(emailId, contactsToLink, "http");
                                
                                Response.Write($"<p style='color:green; font-weight:bold;'>✅ SUCCESS! Enhanced CRM linking completed for email {emailId}!</p>");
                                Response.Write($"<p style='color:green;'>📎 Linked to {contactsToLink.Count} CRM contacts using enhanced logic</p>");
                            }
                        }
                        else
                        {
                            Response.Write("<p style='color:gray'>📭 No CRM contacts found for this email</p>");
                        }
                    }
                    else
                    {
                        Response.Write($"<p style='color:red'>❌ Email {emailId} not found</p>");
                    }
                }
            }
            catch (Exception ex)
            {
                Response.Write($"<p style='color:red'>❌ Error processing email {emailId}: {ex.Message}</p>");
            }
            
            Response.Write("</div>");
        }
    }
    catch (Exception ex)
    {
        Response.Write($"<p style='color:red'>❌ Fatal error: {ex.Message}</p>");
    }
}

private void TestSingleEmail(int emailId)
{
    Response.Write($"<h2>🧪 Testing Single Email: {emailId}</h2>");
    TestEnhancedCrmFunctionality(emailId.ToString());
}

private void TestMonitoringService()
{
    Response.Write("<h2>🧪 Testing Monitoring Service</h2>");
    
    try
    {
        // Try to get status from WebCrmMonitoringService via reflection
        var mailAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ASC.Mail");
        
        if (mailAssembly != null)
        {
            Response.Write("<p>✅ ASC.Mail assembly found</p>");
            
            var serviceType = mailAssembly.GetType("ASC.Mail.Core.Engine.WebCrmMonitoringService");
            if (serviceType != null)
            {
                Response.Write("<p>✅ WebCrmMonitoringService found</p>");
                
                var isRunningProperty = serviceType.GetProperty("IsRunning", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var getStatusMethod = serviceType.GetMethod("GetStatus", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                
                if (isRunningProperty != null)
                {
                    var isRunning = (bool)isRunningProperty.GetValue(null);
                    Response.Write($"<p>📊 Service Running: {isRunning}</p>");
                }
                
                if (getStatusMethod != null)
                {
                    var status = getStatusMethod.Invoke(null, null);
                    Response.Write($"<p>📊 Service Status: {status}</p>");
                }
            }
            else
            {
                Response.Write("<p style='color:orange'>⚠️ WebCrmMonitoringService not found - checking original service</p>");
                
                var oldServiceType = mailAssembly.GetType("ASC.Mail.Core.Engine.CrmEmailAutoLinkService");
                if (oldServiceType != null)
                {
                    Response.Write("<p>✅ CrmEmailAutoLinkService found</p>");
                }
                else
                {
                    Response.Write("<p style='color:red'>❌ No CRM auto-link service found</p>");
                }
            }
        }
        else
        {
            Response.Write("<p style='color:red'>❌ ASC.Mail assembly not found</p>");
        }
    }
    catch (Exception ex)
    {
        Response.Write($"<p style='color:red'>❌ Error testing monitoring service: {ex.Message}</p>");
    }
}
</script>