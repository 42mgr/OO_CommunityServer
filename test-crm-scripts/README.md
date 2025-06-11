# CRM Integration Testing Scripts

This directory contains various testing and debugging scripts for OnlyOffice CRM email auto-linking functionality.

## Service Status & Monitoring

### check-service-status.cs
**Purpose:** Verifies if the enhanced CRM service is available and running
**Usage:** Compile and run to check if CRM services are properly loaded
```bash
csc check-service-status.cs && mono check-service-status.exe
```

### runtime-crm-monitor.cs
**Purpose:** Runtime monitoring of CRM auto-linking service
**Usage:** Monitors CRM service activity in real-time

### enhanced-runtime-crm-monitor.sh
**Purpose:** Shell script for enhanced runtime monitoring
**Usage:** 
```bash
chmod +x enhanced-runtime-crm-monitor.sh
./enhanced-runtime-crm-monitor.sh
```

### start-crm-service.cs
**Purpose:** Starts CRM auto-linking services programmatically
**Usage:** Use to manually start CRM services for testing

## SQL Diagnostics & Validation

### validate-crm-monitoring.sql
**Purpose:** Validates CRM monitoring configuration and data
**Usage:** Run against OnlyOffice database to check CRM setup

### verify_crm_linking.sql
**Purpose:** Verifies that emails are properly linked to CRM contacts
**Usage:** Check if auto-linking is working correctly

### diagnose-crm-autolink.sql
**Purpose:** Comprehensive diagnostics for CRM auto-linking issues
**Usage:** Troubleshoot auto-linking problems

### analyze_unlinked_emails.sql
**Purpose:** Analyzes emails that haven't been linked to CRM contacts
**Usage:** Find emails that should be linked but aren't

### check_duplication_status.sql
**Purpose:** Checks for email duplication issues
**Usage:** Verify that the clean implementation resolved duplication

### link-test-emails.sql
**Purpose:** SQL queries for testing email linking functionality
**Usage:** Test database-level email-to-CRM linking

## Direct Testing Scripts

### test-crm-autolink.sh
**Purpose:** Shell script to test CRM auto-linking functionality
**Usage:**
```bash
chmod +x test-crm-autolink.sh
./test-crm-autolink.sh
```

### test-crm-contact-lookup.cs
**Purpose:** Tests CRM contact lookup functionality
**Usage:** Verify that contacts can be found by email address

### test-direct-crm-link.cs
**Purpose:** Direct testing of CRM linking without services
**Usage:** Test core linking functionality

### test-internal-crm-api.cs
**Purpose:** Tests internal CRM API functionality
**Usage:** Verify CRM API methods work correctly

### test-single-email-link.cs
**Purpose:** Tests linking a single email to CRM contacts
**Usage:** Unit test for single email processing

### manual-crm-linker.cs
**Purpose:** Manual CRM linking utility
**Usage:** Manually link emails to CRM contacts for testing

## Web-based Testing

### test-enhanced-crm.aspx
**Purpose:** Web-based CRM testing interface
**Usage:** Deploy to web directory and access via browser for interactive testing

## Debug & Troubleshooting

### debug-crm-service.cs
**Purpose:** Debugging utility for CRM services
**Usage:** Debug CRM service issues and get detailed logging

## Usage Notes

1. **Database Connection:** Most SQL scripts require connection to the OnlyOffice database
2. **Permissions:** Ensure proper permissions for file access and service operations
3. **Dependencies:** C# scripts may need compilation with appropriate references
4. **Environment:** Scripts assume OnlyOffice is installed in standard locations

## Testing Workflow

1. **Start with service status:** Run `check-service-status.cs` to verify services
2. **Check database:** Use SQL scripts to validate database state
3. **Test functionality:** Use direct testing scripts to verify auto-linking
4. **Monitor runtime:** Use monitoring scripts during testing
5. **Debug issues:** Use debug scripts if problems are found

## Common Testing Scenarios

- **New Installation:** Use `check-service-status.cs` and `validate-crm-monitoring.sql`
- **Auto-linking Issues:** Use `diagnose-crm-autolink.sql` and `analyze_unlinked_emails.sql`
- **Duplication Problems:** Use `check_duplication_status.sql`
- **Performance Testing:** Use `enhanced-runtime-crm-monitor.sh`
- **Manual Verification:** Use `test-single-email-link.cs` and `manual-crm-linker.cs`