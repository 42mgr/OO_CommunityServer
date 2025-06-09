using System;
using System.Linq;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Data.Contracts;
using ASC.Common.Data;
using log4net;

public class TestCrmAutoLink
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(TestCrmAutoLink));
    
    public static string ProcessRecentEmails()
    {
        try
        {
            Log.Info("🔧 TestCrmAutoLink: Starting manual CRM auto-linking test...");
            
            // Get the first tenant
            var tenants = CoreContext.TenantManager.GetTenants();
            if (!tenants.Any())
            {
                return "❌ No tenants found";
            }
            
            var tenant = tenants.First();
            CoreContext.TenantManager.SetCurrentTenant(tenant);
            
            // Query for recent inbox emails without CRM links
            using (var db = new DbManager("mail"))
            {
                var query = @"
                    SELECT m.id, m.from_text, m.to_text, m.cc, m.subject, m.date_received, m.id_user, m.id_mailbox
                    FROM mail_mail m
                    WHERE m.tenant = @tenant 
                      AND m.folder = 2  -- Inbox folder
                      AND m.date_received > DATE_SUB(NOW(), INTERVAL 1 HOUR)
                      AND NOT EXISTS (
                          SELECT 1 FROM crm_relationship_event cre 
                          WHERE cre.entity_type = 0 AND cre.entity_id = m.id
                      )
                    ORDER BY m.date_received DESC
                    LIMIT 5";
                
                var results = db.ExecuteList(query, new { tenant = tenant.TenantId });
                
                if (results.Count == 0)
                {
                    return "ℹ️ No recent unprocessed emails found";
                }
                
                int processed = 0;
                foreach (var result in results)
                {
                    try
                    {
                        var emailId = Convert.ToInt32(result[0]);
                        var fromText = result[1]?.ToString() ?? "";
                        var toText = result[2]?.ToString() ?? "";
                        var userId = result[6]?.ToString() ?? "";
                        var mailboxId = Convert.ToInt32(result[7]);
                        
                        Log.InfoFormat("🔧 TestCrmAutoLink: Processing email {0} from {1}", emailId, fromText);
                        
                        // Set security context
                        SecurityContext.AuthenticateMe(new Guid(userId));
                        
                        // Create message data
                        var message = new MailMessageData
                        {
                            Id = emailId,
                            From = fromText,
                            To = toText,
                            Subject = result[4]?.ToString() ?? "",
                            Date = Convert.ToDateTime(result[5]),
                            UserId = userId,
                            MailboxId = mailboxId
                        };
                        
                        // Create mailbox data
                        var mailbox = new MailBoxData
                        {
                            TenantId = tenant.TenantId,
                            UserId = userId,
                            MailBoxId = mailboxId
                        };
                        
                        // Apply enhanced CRM processing
                        var crmEngine = new CrmLinkEngine(tenant.TenantId, userId, Log);
                        crmEngine.ProcessIncomingEmailForCrm(message, mailbox, "http");
                        
                        processed++;
                        Log.InfoFormat("✅ TestCrmAutoLink: Processed email {0}", emailId);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorFormat("❌ TestCrmAutoLink: Error processing email: {0}", ex.Message);
                    }
                }
                
                return $"✅ Processed {processed} emails with enhanced CRM auto-linking";
            }
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("❌ TestCrmAutoLink: Error in ProcessRecentEmails: {0}", ex.Message);
            return $"❌ Error: {ex.Message}";
        }
    }
}