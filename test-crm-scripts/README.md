# CRM Integration Testing Scripts

This directory contains various testing and debugging scripts for OnlyOffice CRM email auto-linking functionality.

## Service Status & Monitoring

### check-service-status.cs
**Purpose:** Verifies if the enhanced CRM service is available and running
**Status:** ❌ **CANNOT RUN** - Requires C# compiler (csc/mono) not available in container
**Usage:** Compile and run to check if CRM services are properly loaded
```bash
csc check-service-status.cs && mono check-service-status.exe
```
**Alternative:** Use `test-crm-autolink.sh` instead

### runtime-crm-monitor.cs
**Purpose:** Runtime monitoring of CRM auto-linking service
**Usage:** Monitors CRM service activity in real-time

### enhanced-runtime-crm-monitor.sh
**Purpose:** Shell script for enhanced runtime monitoring
**Status:** ✅ **WORKS** - Database-driven CRM monitor with full integration workflow
**Usage:** 
```bash
chmod +x enhanced-runtime-crm-monitor.sh
./enhanced-runtime-crm-monitor.sh
```
**Note:** Contains comprehensive SQL-based monitoring and linking logic

### start-crm-service.cs
**Purpose:** Starts CRM auto-linking services programmatically
**Usage:** Use to manually start CRM services for testing

## SQL Diagnostics & Validation

### validate-crm-monitoring.sql
**Purpose:** Validates CRM monitoring configuration and data
**Status:** ✅ **WORKS** - Database validation queries
**Usage:** Run against OnlyOffice database to check CRM setup

### verify_crm_linking.sql
**Purpose:** Verifies that emails are properly linked to CRM contacts
**Status:** ✅ **WORKS** - Comprehensive verification with statistics
**Usage:** Check if auto-linking is working correctly
**Note:** Shows NO emails currently linked (0/100 test emails)

### diagnose-crm-autolink.sql
**Purpose:** Comprehensive diagnostics for CRM auto-linking issues
**Status:** ✅ **WORKS** - Identifies emails with CRM matches but no links
**Usage:** Troubleshoot auto-linking problems
**Key Finding:** Emails 5054/5053 should be linked to contact 1004 but aren't

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
**Status:** ✅ **WORKS** - Main diagnostic script, finds service logs
**Usage:**
```bash
chmod +x test-crm-autolink.sh
./test-crm-autolink.sh
```
**Key Finding:** Service is running but reports "Ambiguous match found" error

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
**Status:** ❌ **CANNOT RUN** - ASPX pages require application context, not standalone
**Usage:** Deploy to web directory and access via browser for interactive testing
**Alternative:** Use SQL scripts for testing instead

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

1. **Start with service status:** Run `test-crm-autolink.sh` to verify services (check-service-status.cs unavailable)
2. **Check database:** Use SQL scripts to validate database state
3. **Test functionality:** Use SQL scripts and shell scripts to verify auto-linking
4. **Monitor runtime:** Use `enhanced-runtime-crm-monitor.sh` during testing
5. **Debug issues:** Use SQL diagnostic scripts if problems are found

## Current Status Summary (Jun 11, 2025)

### ✅ What's Working:
- **Service Detection**: CRM services are found and started in container
- **Database Access**: All SQL scripts work with correct connection
- **CRM Data**: Contact 1004 exists with email mgrafde@gmail.com
- **Email Processing**: Recent test emails are being received

### ❌ Current Issues:
- **Email Duplication**: Test 46 appears twice (ID 5053 inbox, ID 5054 sent) within 2 seconds
- **Service Conflict**: Multiple mail services running simultaneously:
  - MailAggregator: ASC.Mail.Core.dll (1,544,704 bytes) - original service
  - WebStudio: ASC.Mail.dll (881,664 bytes) - our enhanced version
  - TeamLabSvc: ASC.Mail.dll (881,664 bytes) - our enhanced version
- **"Ambiguous match found"**: Both services processing same emails causes conflicts
- **No Auto-Linking**: 0 out of 100 test emails are linked to CRM due to conflicts

### 🔧 Root Cause Analysis:
The issue is architectural - we have **competing mail processing services**:
1. Original MailAggregator service processes emails using ASC.Mail.Core.dll
2. Our enhanced web service also processes emails using ASC.Mail.dll
3. Both try to process the same emails → duplication + conflicts

### 🚨 Applied Conflict Fixes:
**Modified timing and approach to coexist with MailAggregator service:**
- Changed CRM service timing: 2-minute startup delay, 60-second intervals
- Added 30-second buffer: only process emails older than 30 seconds
- Removed problematic `GetMessage` calls that caused database conflicts
- Fixed reflection issues in WebCrmMonitoringService
- Service now only reads from database, doesn't compete for email saving

**Next**: Test if these changes eliminate duplication and enable CRM linking

## Common Testing Scenarios

- **New Installation:** Use `check-service-status.cs` and `validate-crm-monitoring.sql`
- **Auto-linking Issues:** Use `diagnose-crm-autolink.sql` and `analyze_unlinked_emails.sql`
- **Duplication Problems:** Use `check_duplication_status.sql`
- **Performance Testing:** Use `enhanced-runtime-crm-monitor.sh`
- **Manual Verification:** Use `test-single-email-link.cs` and `manual-crm-linker.cs`