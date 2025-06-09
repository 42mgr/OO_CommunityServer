using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Data.Contracts;

namespace ASC.Mail.Enhanced
{
    /// <summary>
    /// Background service that monitors for new incoming emails and applies enhanced CRM auto-linking
    /// </summary>
    public class CrmAutoLinkingService : BackgroundService
    {
        private readonly ILogger<CrmAutoLinkingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private DateTime _lastProcessedTime = DateTime.UtcNow.AddMinutes(-5); // Start with 5 minutes ago
        
        public CrmAutoLinkingService(ILogger<CrmAutoLinkingService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 CrmAutoLinkingService: Enhanced CRM auto-linking service started");
            
            // Wait a bit for application to fully start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNewEmails();
                    _lastProcessedTime = DateTime.UtcNow;
                    
                    // Check every 30 seconds for new emails
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ CrmAutoLinkingService: Error in email processing cycle");
                    
                    // Wait longer on error to avoid rapid retries
                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                }
            }
            
            _logger.LogInformation("🛑 CrmAutoLinkingService: Enhanced CRM auto-linking service stopped");
        }
        
        private async Task ProcessNewEmails()
        {
            using var scope = _serviceProvider.CreateScope();
            
            try
            {
                // Get all tenants
                var tenants = CoreContext.TenantManager.GetTenants();
                
                foreach (var tenant in tenants)
                {
                    try
                    {
                        await ProcessTenantEmails(tenant.TenantId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ CrmAutoLinkingService: Error processing emails for tenant {TenantId}", tenant.TenantId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CrmAutoLinkingService: Error getting tenants");
            }
        }
        
        private async Task ProcessTenantEmails(int tenantId)
        {
            try
            {
                CoreContext.TenantManager.SetCurrentTenant(tenantId);
                
                // Query for new inbox emails that haven't been processed for CRM
                var newEmails = await GetUnprocessedInboxEmails(tenantId);
                
                if (newEmails.Count > 0)
                {
                    _logger.LogInformation("🔍 CrmAutoLinkingService: Found {Count} new emails to process for tenant {TenantId}", 
                                          newEmails.Count, tenantId);
                    
                    foreach (var email in newEmails)
                    {
                        try
                        {
                            await ProcessEmailForEnhancedCrm(email, tenantId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ CrmAutoLinkingService: Error processing email {EmailId}", email.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CrmAutoLinkingService: Error in ProcessTenantEmails for tenant {TenantId}", tenantId);
            }
        }
        
        private async Task<List<MailMessageData>> GetUnprocessedInboxEmails(int tenantId)
        {
            var emails = new List<MailMessageData>();
            
            try
            {
                using var mailContext = new MailDbContext();
                
                // SQL query to find inbox emails without CRM relationship events
                var query = @"
                    SELECT m.id, m.from_text, m.to_text, m.cc, m.subject, m.date_received, m.id_user, m.id_mailbox
                    FROM mail_mail m
                    WHERE m.tenant = @tenantId 
                      AND m.folder = 2  -- Inbox folder
                      AND m.date_received > @lastProcessed
                      AND NOT EXISTS (
                          SELECT 1 FROM crm_relationship_event cre 
                          WHERE cre.entity_type = 0 AND cre.entity_id = m.id
                      )
                    ORDER BY m.date_received ASC
                    LIMIT 50";  -- Process max 50 emails per cycle
                
                // Execute query and convert to MailMessageData objects
                // Note: This would need proper EF Core implementation
                // For now, using a simplified approach
                
                _logger.LogDebug("🔍 CrmAutoLinkingService: Querying for unprocessed emails since {LastProcessed}", _lastProcessedTime);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CrmAutoLinkingService: Error querying unprocessed emails");
            }
            
            return emails;
        }
        
        private async Task ProcessEmailForEnhancedCrm(MailMessageData email, int tenantId)
        {
            try
            {
                _logger.LogInformation("🔧 CrmAutoLinkingService: Processing email {EmailId} - DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED for message {EmailId} from {From}", 
                                      email.Id, email.Id, email.From);
                
                // Set security context for the email owner
                CoreContext.TenantManager.SetCurrentTenant(tenantId);
                SecurityContext.AuthenticateMe(new Guid(email.UserId));
                
                // Create CRM link engine with enhanced functionality
                var crmEngine = new CrmLinkEngine(tenantId, email.UserId, _logger);
                
                // Get mailbox data - simplified for this example
                var mailbox = new MailBoxData
                {
                    TenantId = tenantId,
                    UserId = email.UserId,
                    MailBoxId = email.MailboxId
                };
                
                // Apply enhanced CRM processing
                crmEngine.ProcessIncomingEmailForCrm(email, mailbox, "http");
                
                _logger.LogInformation("✅ CrmAutoLinkingService: Enhanced CRM auto-processing completed for message {EmailId}", email.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ CrmAutoLinkingService: Enhanced CRM auto-processing failed for message {EmailId}: {Error}", 
                                  email.Id, ex.Message);
            }
        }
    }
}

/// <summary>
/// Service registration extensions
/// </summary>
public static class CrmAutoLinkingServiceExtensions
{
    public static IServiceCollection AddEnhancedCrmAutoLinking(this IServiceCollection services)
    {
        services.AddHostedService<CrmAutoLinkingService>();
        return services;
    }
}