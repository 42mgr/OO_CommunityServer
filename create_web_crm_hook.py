#!/usr/bin/env python3
"""
Create a web application hook for enhanced CRM processing of incoming emails
"""

import sys

def create_crm_processing_hook():
    """Create a web application component that monitors and processes emails for CRM"""
    
    hook_code = '''using System;
using System.Threading;
using System.Threading.Tasks;
using ASC.Core;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Dao.Entities;
using log4net;

namespace ASC.Mail.Enhanced
{
    /// <summary>
    /// Enhanced CRM processing hook for incoming emails
    /// </summary>
    public class EnhancedCrmProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EnhancedCrmProcessor));
        private static Timer _timer;
        private static DateTime _lastProcessedTime = DateTime.UtcNow;
        
        public static void Initialize()
        {
            Log.Info("EnhancedCrmProcessor: Initializing enhanced CRM auto-processing...");
            
            // Start background timer to check for new emails every 30 seconds
            _timer = new Timer(ProcessNewEmails, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            Log.Info("EnhancedCrmProcessor: Enhanced CRM processing initialized successfully");
        }
        
        private static void ProcessNewEmails(object state)
        {
            try
            {
                Log.Debug("EnhancedCrmProcessor: Checking for new emails to process...");
                
                // Get all tenants and process their emails
                var tenants = CoreContext.TenantManager.GetTenants();
                
                foreach (var tenant in tenants)
                {
                    try
                    {
                        CoreContext.TenantManager.SetCurrentTenant(tenant);
                        ProcessTenantEmails(tenant.TenantId);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorFormat("EnhancedCrmProcessor: Error processing emails for tenant {0}: {1}", tenant.TenantId, ex.Message);
                    }
                }
                
                _lastProcessedTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("EnhancedCrmProcessor: Error in ProcessNewEmails: {0}", ex.Message);
            }
        }
        
        private static void ProcessTenantEmails(int tenantId)
        {
            // Query for new unprocessed emails in inbox since last check
            using (var db = new ASC.Mail.Core.DbManager())
            {
                var newEmails = db.ExecuteList(
                    "SELECT id, from_text, to_text, subject, date_received, id_user " +
                    "FROM mail_mail " +
                    "WHERE tenant = @tenant AND folder = 2 AND date_received > @lastCheck " +
                    "AND id NOT IN (SELECT DISTINCT entity_id FROM crm_relationship_event WHERE entity_type = 0)",
                    new { tenant = tenantId, lastCheck = _lastProcessedTime }
                );
                
                foreach (var email in newEmails)
                {
                    try
                    {
                        ProcessEmailForCrm(email, tenantId);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorFormat("EnhancedCrmProcessor: Error processing email {0}: {1}", email.id, ex.Message);
                    }
                }
                
                if (newEmails.Count > 0)
                {
                    Log.InfoFormat("EnhancedCrmProcessor: Processed {0} new emails for tenant {1}", newEmails.Count, tenantId);
                }
            }
        }
        
        private static void ProcessEmailForCrm(dynamic email, int tenantId)
        {
            Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED for message {0} from {1}", email.id, email.from_text);
            
            try
            {
                // Set security context for the user
                CoreContext.TenantManager.SetCurrentTenant(tenantId);
                SecurityContext.AuthenticateMe(new Guid(email.id_user));
                
                // Create CRM link engine and process
                var crmEngine = new CrmLinkEngine(tenantId, new Guid(email.id_user), Log);
                
                // Create a basic MailMessageData object for processing
                var message = new MailMessageData
                {
                    Id = email.id,
                    From = email.from_text,
                    To = email.to_text,
                    Subject = email.subject,
                    Date = email.date_received
                };
                
                // Process with enhanced CRM logic
                crmEngine.ProcessIncomingEmailForCrm(message, null, "http");
                
                Log.InfoFormat("DEBUG: CRM auto-processing completed for message {0}", email.id);
            }
            catch (Exception ex)
            {
                Log.WarnFormat("DEBUG: CRM auto-processing failed for message {0}: {1}", email.id, ex.Message);
            }
        }
    }
}'''
    
    return hook_code

def main():
    print("🔧 Creating web application CRM processing hook...")
    
    hook_code = create_crm_processing_hook()
    
    # Write the hook to a C# file
    with open("/root/claude/OO_CommunityServer/EnhancedCrmProcessor.cs", "w") as f:
        f.write(hook_code)
    
    print("✅ Created EnhancedCrmProcessor.cs")
    print("📋 Next steps:")
    print("   1. Compile this into the web application")
    print("   2. Initialize it in Application_Start")
    print("   3. Deploy to OnlyOffice web application")
    
if __name__ == "__main__":
    main()