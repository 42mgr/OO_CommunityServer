# CRM Email Monitoring Integration Guide

This guide explains how to integrate the CRM Email Monitoring Job with your ONLYOFFICE Community Server to automatically link incoming and outgoing emails to CRM contacts.

## Overview

The CRM Email Monitoring system consists of several components that work together to monitor the `mail_mail` table for new emails and automatically link them to CRM contacts when email address matches are found.

## Components

1. **CrmEmailMonitoringJob.cs** - Core monitoring logic with database queries and CRM linking
2. **CrmEmailMonitoringService.cs** - Background service wrapper for hosting environments
3. **CrmEmailMonitoringStartup.cs** - Startup hooks for web applications
4. **TestCrmEmailMonitoring.cs** - Test console application
5. **CrmEmailMonitoringConfig.json** - Configuration file

## Integration Options

### Option 1: Web Application Integration (Recommended)

Add the monitoring to your existing ONLYOFFICE web application:

#### Step 1: Add Files to Project
Copy these files to your ONLYOFFICE project:
- `CrmEmailMonitoringJob.cs`
- `CrmEmailMonitoringService.cs` 
- `CrmEmailMonitoringStartup.cs`

#### Step 2: Update Global.asax.cs
Add this code to your `Global.asax.cs` file:

```csharp
using ASC.Mail.Enhanced;

public class Global : HttpApplication
{
    protected void Application_Start()
    {
        // Your existing code...
        
        // Add CRM Email Monitoring
        try
        {
            log4net.LogManager.GetLogger(typeof(Global)).Info("🚀 Starting CRM Email Monitoring...");
            CrmEmailMonitoringInitializer.Initialize();
            log4net.LogManager.GetLogger(typeof(Global)).Info("✅ CRM Email Monitoring started successfully");
        }
        catch (Exception ex)
        {
            log4net.LogManager.GetLogger(typeof(Global)).Error("❌ Error starting CRM Email Monitoring", ex);
        }
    }
    
    protected void Application_End()
    {
        // Add before your existing code...
        try
        {
            log4net.LogManager.GetLogger(typeof(Global)).Info("🛑 Stopping CRM Email Monitoring...");
            CrmEmailMonitoringInitializer.Shutdown();
        }
        catch (Exception ex)
        {
            log4net.LogManager.GetLogger(typeof(Global)).Error("❌ Error stopping CRM Email Monitoring", ex);
        }
        
        // Your existing code...
    }
}
```

#### Step 3: Update Database Connection
Modify the `GetConnectionString()` method in `CrmEmailMonitoringJob.cs` to match your database configuration:

```csharp
private static string GetConnectionString()
{
    // Option 1: Use existing configuration
    return ConfigurationManager.ConnectionStrings["mail"].ConnectionString;
    
    // Option 2: Docker default
    // return "Server=localhost;Port=3306;Database=onlyoffice;Uid=onlyoffice_user;Pwd=onlyoffice_pass;CharSet=utf8;";
    
    // Option 3: Custom configuration
    // return "Server=your-server;Database=your-db;Uid=your-user;Pwd=your-pass;CharSet=utf8;";
}
```

### Option 2: HTTP Module Integration

Add the HTTP module to your `web.config`:

```xml
<system.web>
  <httpModules>
    <!-- Your existing modules -->
    <add name="CrmEmailMonitoringStartup" type="ASC.Mail.Enhanced.CrmEmailMonitoringStartupModule" />
  </httpModules>
</system.web>

<!-- For IIS 7+ -->
<system.webServer>
  <modules>
    <!-- Your existing modules -->
    <add name="CrmEmailMonitoringStartup" type="ASC.Mail.Enhanced.CrmEmailMonitoringStartupModule" />
  </modules>
</system.webServer>
```

### Option 3: Standalone Service

Run the monitoring as a separate service:

#### Step 1: Create Service Project
Create a new .NET Core console application or Windows Service project.

#### Step 2: Add Service Host
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ASC.Mail.Enhanced;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddHostedService<CrmEmailMonitoringService>();
            })
            .Build();
            
        await host.RunAsync();
    }
}
```

#### Step 3: Deploy as SystemD Service (Linux)
Create `/etc/systemd/system/crm-email-monitoring.service`:

```ini
[Unit]
Description=CRM Email Monitoring Service
After=network.target

[Service]
Type=notify
User=onlyoffice
WorkingDirectory=/var/www/onlyoffice
ExecStart=/usr/bin/dotnet /var/www/onlyoffice/CrmEmailMonitoring.dll
Restart=on-failure
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable crm-email-monitoring
sudo systemctl start crm-email-monitoring
sudo systemctl status crm-email-monitoring
```

## Configuration

### Database Connection

Update the connection string in `CrmEmailMonitoringJob.cs` to match your setup:

**For Docker Compose (default):**
```csharp
"Server=mysql;Port=3306;Database=onlyoffice;Uid=onlyoffice_user;Pwd=onlyoffice_pass;CharSet=utf8;"
```

**For local MySQL:**
```csharp
"Server=localhost;Port=3306;Database=onlyoffice;Uid=root;Pwd=your_password;CharSet=utf8;"
```

### Monitoring Parameters

You can adjust these values in `CrmEmailMonitoringJob.cs`:

```csharp
// Check interval (currently 30 seconds)
TimeSpan.FromSeconds(30)

// Batch size (currently 100 emails per cycle)
LIMIT 100

// Initial lookback (currently 5 minutes)
DateTime.UtcNow.AddMinutes(-5)
```

## How It Works

### 1. Email Detection
The job queries for emails that:
- Are in Inbox (folder=2) or Sent (folder=1) folders
- Were received after the last processing time
- Don't have existing CRM relationship events

### 2. Email Address Extraction
Extracts email addresses from:
- `from_text` field
- `to_text` field  
- `cc` field
- `bcc` field

### 3. CRM Contact Matching
Searches `crm_contact_info` table for contacts with matching email addresses (type=1).

### 4. Link Creation
For matches, creates:
- **mail_chain_x_crm_entity** entry (links email chain to CRM contact)
- **crm_relationship_event** entry (creates relationship event)
- Updates **mail_chain** to mark as CRM-linked

### 5. No-Match Handling
For emails without matches, creates a special relationship event to avoid reprocessing.

## Testing

### 1. Console Test Application
Compile and run `TestCrmEmailMonitoring.cs`:

```bash
# Compile
mcs -out:TestCrmEmailMonitoring.exe TestCrmEmailMonitoring.cs CrmEmailMonitoringJob.cs \
    -r:System.Data.dll -r:MySql.Data.dll -r:log4net.dll -r:Newtonsoft.Json.dll

# Run
mono TestCrmEmailMonitoring.exe
```

### 2. Manual Database Test
Check for unprocessed emails:

```sql
-- Find emails without CRM events
SELECT COUNT(*) as unprocessed_emails
FROM mail_mail m
WHERE m.folder IN (1, 2)
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  );

-- Check recent processing
SELECT COUNT(*) as recent_events
FROM crm_relationship_event 
WHERE entity_type = 0 
  AND create_on > DATE_SUB(NOW(), INTERVAL 1 HOUR);
```

### 3. Log Monitoring
Monitor the log files for processing activity:

```bash
# View logs
tail -f /var/log/onlyoffice/mail.log | grep "CrmEmailMonitoring"

# Or application logs
tail -f /var/log/onlyoffice/web.log | grep "CrmEmailMonitoring"
```

## Troubleshooting

### Common Issues

#### 1. Database Connection Errors
- Verify connection string is correct
- Check MySQL user permissions
- Ensure database name matches your setup

#### 2. No Emails Being Processed
- Check if emails exist in `mail_mail` table
- Verify folder values (1=Sent, 2=Inbox)
- Check if relationship events already exist

#### 3. CRM Contacts Not Found
- Verify CRM contacts have email addresses in `crm_contact_info`
- Check that `type = 1` for email addresses
- Ensure email addresses match exactly (case-insensitive)

#### 4. Permission Errors
- Check file permissions for log files
- Verify database user has INSERT/UPDATE permissions
- Ensure application has access to configuration files

### Debug Queries

```sql
-- Check email data
SELECT id, from_text, to_text, subject, date_received, folder
FROM mail_mail 
ORDER BY date_received DESC 
LIMIT 10;

-- Check CRM contact emails
SELECT c.display_name, ci.data as email, ci.type
FROM crm_contact c
JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE ci.type = 1  -- Email type
LIMIT 10;

-- Check existing relationships
SELECT cre.*, c.display_name
FROM crm_relationship_event cre
LEFT JOIN crm_contact c ON cre.contact_id = c.id
WHERE cre.entity_type = 0
ORDER BY cre.create_on DESC
LIMIT 10;
```

## Performance Considerations

### 1. Monitoring Frequency
- Default: 30 seconds
- Recommended: 30-60 seconds for most installations
- High volume: Consider 15-30 seconds
- Low volume: Can increase to 60-120 seconds

### 2. Batch Size
- Default: 100 emails per cycle
- High volume: Increase to 200-500
- Low memory: Decrease to 50-100

### 3. Database Indexing
Ensure these indexes exist for optimal performance:

```sql
-- Indexes for mail_mail table
CREATE INDEX idx_mail_mail_date_folder ON mail_mail (date_received, folder);
CREATE INDEX idx_mail_mail_tenant_folder ON mail_mail (tenant, folder, date_received);

-- Indexes for crm_relationship_event table
CREATE INDEX idx_crm_rel_event_entity ON crm_relationship_event (entity_type, entity_id);

-- Indexes for crm_contact_info table
CREATE INDEX idx_crm_contact_info_type_data ON crm_contact_info (type, data);
```

## Security Considerations

1. **Database Permissions**: Use a dedicated database user with minimal required permissions
2. **Connection Security**: Use SSL connections for remote databases
3. **Logging**: Avoid logging sensitive email content or personal information
4. **Error Handling**: Prevent error messages from exposing database structure

## Support and Maintenance

### Monitoring Health
- Check log files regularly for errors
- Monitor processing volume and performance
- Set up alerts for database connection failures

### Updates and Maintenance
- Review and update connection strings when database changes
- Monitor for new ONLYOFFICE updates that might affect table structures
- Backup configuration files before system updates

## Integration Checklist

- [ ] Copy monitoring job files to project
- [ ] Update database connection string
- [ ] Add startup code to Global.asax.cs or HTTP module
- [ ] Test database connectivity
- [ ] Verify email and CRM data exists
- [ ] Run test application
- [ ] Monitor logs for processing activity
- [ ] Verify CRM relationships are being created
- [ ] Set up monitoring and alerting
- [ ] Document any customizations