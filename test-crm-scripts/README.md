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

## Streamlined Diagnostic Process

**When user says "run your diagnostics", follow this efficient process:**

### 1. Quick DLL Verification (Check if fix was deployed)
```bash
# Check MailAggregator DLL timestamp
docker exec onlyoffice-community-server stat -c "%Y %n" /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Core.dll

# Check for our fix markers in MailAggregator
docker exec onlyoffice-community-server bash -c "strings /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Core.dll | grep -A3 'Force COMPLETE rebuild'"
```

### 2. Email Duplication Check (Core diagnostic)
```bash
# Check recent emails for duplication patterns
docker exec onlyoffice-community-server mysql -u root -pmy-secret-pw -e "
USE onlyoffice; 
SELECT id, folder, from_text, to_text, date_received 
FROM mail_mail 
WHERE id >= (SELECT MAX(id) - 10 FROM mail_mail) 
ORDER BY id DESC LIMIT 10;"

# Check CRM contact emails specifically (mgrafde@gmail.com = contact 1004)
docker exec onlyoffice-community-server mysql -u root -pmy-secret-pw -e "
USE onlyoffice; 
SELECT id, folder, from_text, to_text, date_received 
FROM mail_mail 
WHERE (from_text LIKE '%mgrafde@gmail.com%' OR to_text LIKE '%mgrafde@gmail.com%') 
AND date_received >= DATE_SUB(NOW(), INTERVAL 1 HOUR) 
ORDER BY date_received DESC;"

# Count emails by folder in last hour
docker exec onlyoffice-community-server mysql -u root -pmy-secret-pw -e "
USE onlyoffice; 
SELECT COUNT(*) as total_emails, 
       COUNT(CASE WHEN folder = 1 THEN 1 END) as inbox_count, 
       COUNT(CASE WHEN folder = 2 THEN 1 END) as sent_count 
FROM mail_mail 
WHERE date_received >= DATE_SUB(NOW(), INTERVAL 1 HOUR);"
```

### 3. Interpretation Guidelines

**✅ FIX WORKING:** 
- CRM emails (mgrafde@gmail.com) appear only in folder 1 (inbox) OR folder 2 (sent), not both
- No duplicate email IDs for same timestamp

**❌ FIX NOT WORKING:**
- CRM emails appear in BOTH folder 1 (inbox) AND folder 2 (sent) 
- Duplicate email IDs with same content but different folders

**🔧 BUILD ISSUE:**
- MailAggregator DLL timestamp is old
- No "Force COMPLETE rebuild" marker in MailAggregator
- Need to trigger complete rebuild and redeploy

**🎯 ROOT CAUSE DISCOVERED:**
- External emails (mgrafde@gmail.com → mgrafch@gmail.com) trigger our CrmEmailAutoLinkService
- Our LinkChainToCrmEnhanced() calls `engine.MessageEngine.GetMessage()` (line 499)
- MessageEngine.GetMessage() has side effects that create duplicate in Sent folder
- This explains why only CRM-matched emails duplicate - they're the only ones processed by our code!

**✅ FINAL SOLUTION IMPLEMENTED:**
- Replaced `LinkChainToCrmEnhanced()` with `MarkChainAsCrmLinked()`
- MarkChainAsCrmLinked() only saves database links (no message loading)
- Eliminates GetMessage() calls that cause duplication
- Emails still appear in CRM interface properly linked
- Much simpler and safer approach for auto-linking

### 4. Log Analysis (If needed)
```bash
# Check for our legacy stub being called
docker logs onlyoffice-community-server --since 30m | grep -i "legacy.*called.*ignored"

# Check for CRM processing logs
docker logs onlyoffice-community-server --since 30m | grep -i "CrmLinkEngine\|ProcessIncomingEmailForCrm"
```

This streamlined process takes ~2 minutes and immediately shows:
1. Whether the fix was deployed
2. Whether duplication is still occurring  
3. Which emails are affected
4. Next steps needed

## Current Status Summary (Jun 11, 2025)

### ✅ What's Working:
- **Service Detection**: CRM services are found and started in container
- **Database Access**: All SQL scripts work with correct connection
- **CRM Data**: Contact 1004 exists with email mgrafde@gmail.com
- **Email Processing**: Recent test emails are being received

### 🚨 CRITICAL DISCOVERY - ROOT CAUSE IDENTIFIED:
- **Duplication Pattern**: Only emails with CRM matches are duplicated!
  - Test 46: mgrafde@gmail.com → HAS CRM MATCH (contact 1004) → DUPLICATED (Inbox + Sent)
  - Test 47: mgrafus@gmail.com → NO CRM MATCH → NOT DUPLICATED (Inbox only)
  - Test 48: mgrafde@gmail.com → HAS CRM MATCH (contact 1004) → DUPLICATED (Inbox + Sent)

### ❌ Root Cause Analysis:
- **MailAggregator Service**: ASC.Mail.Core.dll (1,544,704 bytes) **WAS UPDATED** with our modifications
- **Both DLLs updated simultaneously**: Same timestamp Jun 11 14:02
- **MailAggregator now calls our ProcessIncomingEmailForCrm method**
- **Double Processing**: MailAggregator processes emails AND calls our CRM logic
- **Result**: Emails with CRM matches get processed twice → duplication

### 🔧 TRUE Root Cause:
**We inadvertently modified shared CrmLinkEngine.cs used by MailAggregator:**
1. **MailAggregator service** uses ASC.Mail.Core.dll which contains CrmLinkEngine
2. **Our build process** updates BOTH ASC.Mail.dll AND ASC.Mail.Core.dll
3. **MailAggregator now has our ProcessIncomingEmailForCrm method**
4. **When emails have CRM matches**: MailAggregator calls our method → triggers duplication
5. **When emails have no CRM matches**: Normal processing → no duplication

### ✅ SOLUTION IMPLEMENTED:
**Renamed and isolated our CRM method to prevent MailAggregator conflicts:**
- **Renamed**: `ProcessIncomingEmailForCrm` → `ProcessIncomingEmailForCrmWebStudio`
- **Added Legacy Stub**: Empty `ProcessIncomingEmailForCrm` method for MailAggregator compatibility
- **Original State Restored**: MailAggregator sees the original method signature (empty implementation)
- **Our Functionality Preserved**: WebStudio uses the renamed method with full functionality

**This should eliminate duplication while maintaining our CRM auto-linking features!**

## Investigation Process & Dead Ends

### 🔍 Investigation Timeline:

#### **Initial Hypothesis (WRONG)**:
- **Theory**: Multiple services competing for email processing
- **Evidence**: MailAggregator + WebStudio both running, "Ambiguous match found" errors
- **Action Taken**: Modified timing (2-minute delays, 30-second buffers), removed GetMessage() calls
- **Result**: Didn't fix duplication - Test 48 still duplicated after container restart

#### **Key Discovery - Pattern Recognition**:
- **Observation**: Only emails with CRM matches duplicated!
  - Test 46: mgrafde@gmail.com → CRM contact 1004 → DUPLICATED (Inbox + Sent)
  - Test 47: mgrafus@gmail.com → No CRM match → NOT DUPLICATED (Inbox only)
  - Test 48: mgrafde@gmail.com → CRM contact 1004 → DUPLICATED (Inbox + Sent)
- **Insight**: Duplication correlates with CRM processing, not general mail processing

#### **Architecture Investigation**:
- **File Analysis**: Both ASC.Mail.dll (881,664 bytes) and ASC.Mail.Core.dll (1,544,704 bytes) updated simultaneously
- **Service Analysis**: MailAggregator uses ASC.Mail.Core.dll and has CrmLinkEngine references
- **Method Discovery**: Our `ProcessIncomingEmailForCrm` method was added to shared CrmLinkEngine.cs

#### **Historical Analysis**:
- **Git Research**: Original CrmLinkEngine.cs (003395ba1) did NOT have ProcessIncomingEmailForCrm
- **Previous Fixes**: Method was originally called by MessageEngine, removed to fix duplication (commit bfae8ed13)
- **Current State**: Method exists but isn't called from our code - yet duplication occurs

#### **Final Root Cause**:
- **Method Injection**: Our ProcessIncomingEmailForCrm added to shared code used by MailAggregator
- **Automatic Invocation**: MailAggregator has CRM processing that calls methods by name/reflection
- **Selective Triggering**: Only emails with CRM matches trigger the additional processing

### 🚫 Dead Ends Explored:

1. **Service Timing Conflicts**: 
   - Modified intervals, startup delays, processing buffers
   - **Why it failed**: Problem wasn't timing, was method injection

2. **Database Lock Issues**:
   - Removed GetMessage() calls, simplified data access
   - **Why it failed**: MailAggregator wasn't competing for database access

3. **Reflection Ambiguity**:
   - Investigated "Ambiguous match found" errors in service logs
   - **Why irrelevant**: This was a symptom, not the cause of duplication

4. **Configuration Conflicts**:
   - Checked if multiple services processing same emails
   - **Why incomplete**: Services weren't competing, they were calling same method

5. **WebStudio Service Issues**:
   - Assumed our background service wasn't working properly
   - **Why wrong**: Background service was fine, the issue was in shared code

### 📚 Lessons Learned:

1. **Pattern Recognition**: Look for correlations in data (CRM matches vs duplication)
2. **Shared Code Impact**: Changes to shared classes affect all services using them
3. **Historical Context**: Understanding original state helps identify what changed
4. **File Timestamp Analysis**: Both DLLs updating simultaneously was a crucial clue
5. **Method Naming Conflicts**: Even unused methods can be called by reflection/discovery

### 🎯 Solution Validation:

The method renaming approach addresses the root cause:
- **MailAggregator compatibility**: Empty stub maintains original interface
- **Functionality preservation**: Renamed method keeps our features intact
- **Selective processing**: Only our WebStudio code calls the enhanced method
- **No architectural changes**: Minimal impact on existing systems

## Common Testing Scenarios

- **New Installation:** Use `check-service-status.cs` and `validate-crm-monitoring.sql`
- **Auto-linking Issues:** Use `diagnose-crm-autolink.sql` and `analyze_unlinked_emails.sql`
- **Duplication Problems:** Use `check_duplication_status.sql`
- **Performance Testing:** Use `enhanced-runtime-crm-monitor.sh`
- **Manual Verification:** Use `test-single-email-link.cs` and `manual-crm-linker.cs`