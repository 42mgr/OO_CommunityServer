#!/bin/bash

# Test CRM Auto-Linking Service
# This script helps diagnose why emails aren't being auto-linked

echo "🔍 CRM Auto-Linking Service Diagnostic"
echo "======================================"

# Check if we can access the database
echo ""
echo "📊 Running diagnostic queries..."

# Check if mysql is available
if command -v mysql &> /dev/null; then
    echo "✅ MySQL client found"
    
    # Try to read database connection info from config
    CONFIG_FILE="/var/www/onlyoffice/WebStudio/web.connections.config"
    if [ -f "$CONFIG_FILE" ]; then
        echo "✅ Found config file: $CONFIG_FILE"
        echo "📋 Database connection info:"
        grep -i "server\|database\|user" "$CONFIG_FILE" | head -5
    else
        echo "⚠️ Config file not found at $CONFIG_FILE"
        echo "Please check: /etc/onlyoffice/ or /var/www/onlyoffice/ for config files"
    fi
    
    echo ""
    echo "📝 To run diagnostic queries manually:"
    echo "mysql -u [username] -p [database] < diagnose-crm-autolink.sql"
    
else
    echo "❌ MySQL client not found"
    echo "Please install mysql-client or access database directly"
fi

echo ""
echo "🔍 Checking application logs for CRM service..."

# Check common log locations
LOG_LOCATIONS=(
    "/var/log/onlyoffice/"
    "/var/www/onlyoffice/Logs/"
    "/opt/onlyoffice/communityserver/Logs/"
    "/usr/share/onlyoffice/communityserver/logs/"
)

for location in "${LOG_LOCATIONS[@]}"; do
    if [ -d "$location" ]; then
        echo "✅ Found log directory: $location"
        
        # Look for recent CRM service messages
        echo "🔍 Searching for CRM Auto-Link messages..."
        find "$location" -name "*.log" -mtime -1 -exec grep -l "CrmEmailAutoLinkService\|Auto-linked email" {} \; 2>/dev/null | head -3
        
        # Show recent CRM-related log entries
        echo "📋 Recent CRM service log entries:"
        find "$location" -name "*.log" -mtime -1 -exec grep -h "CrmEmailAutoLinkService\|Auto-linked email" {} \; 2>/dev/null | tail -10
        
        break
    fi
done

echo ""
echo "🧪 Manual Testing Steps:"
echo "========================"
echo "1. Check if service started: Look for 'CRM Email Auto-Link Service started successfully' in logs"
echo "2. Send test email to/from a known CRM contact email address"
echo "3. Wait 30-60 seconds for processing"
echo "4. Check logs for: 'Found X unprocessed emails' and 'Auto-linked email X to CRM'"
echo "5. Verify in CRM interface that email appears in contact history"

echo ""
echo "🔧 Troubleshooting Tips:"
echo "========================"
echo "• Service runs every 30 seconds"
echo "• Only links emails to existing CRM contacts with matching email addresses" 
echo "• Check that CRM contacts have email addresses in contact info"
echo "• Verify tenant/user permissions"
echo "• Service uses reflection to avoid circular dependencies"

echo ""
echo "📂 Key Files:"
echo "============="
echo "• Service: ASC.Mail.dll (contains CrmEmailAutoLinkService)"
echo "• Startup: web/studio/ASC.Web.Studio/Startup.cs"
echo "• Shutdown: web/studio/ASC.Web.Studio/Global.asax.cs"
echo "• Database: mail_mail, crm_contact, mail_chain_x_crm_entity tables"