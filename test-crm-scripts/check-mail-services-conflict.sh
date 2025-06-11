#!/bin/bash

echo "🔍 OnlyOffice Mail Services Conflict Analysis"
echo "============================================="

echo ""
echo "📋 Running Mail Services:"
ps aux | grep -E "(MailAggregator|MailCleaner|MailImap|MailWatchdog)" | grep -v grep

echo ""
echo "📄 Mail DLL Versions Analysis:"
echo ""

for service in MailAggregator MailCleaner MailImap MailWatchdog TeamLabSvc; do
    echo "🔍 $service Service:"
    service_path="/var/www/onlyoffice/Services/$service"
    
    if [ -d "$service_path" ]; then
        if [ -f "$service_path/ASC.Mail.Core.dll" ]; then
            size=$(stat -c%s "$service_path/ASC.Mail.Core.dll")
            date=$(stat -c%y "$service_path/ASC.Mail.Core.dll")
            echo "  📦 ASC.Mail.Core.dll: $size bytes, $date"
        fi
        
        if [ -f "$service_path/ASC.Mail.dll" ]; then
            size=$(stat -c%s "$service_path/ASC.Mail.dll")
            date=$(stat -c%y "$service_path/ASC.Mail.dll")
            echo "  📦 ASC.Mail.dll: $size bytes, $date"
        fi
    else
        echo "  ❌ Service directory not found"
    fi
    echo ""
done

echo "🌐 WebStudio Mail DLL:"
if [ -f "/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll" ]; then
    size=$(stat -c%s "/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll")
    date=$(stat -c%y "/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll")
    echo "  📦 ASC.Mail.dll: $size bytes, $date"
fi

echo ""
echo "🔍 Email Duplication Check (last 2 hours):"
mysql -h onlyoffice-mysql-server -u onlyoffice_user -ponlyoffice_pass onlyoffice -se "
    SELECT 
        subject,
        COUNT(*) as count,
        GROUP_CONCAT(CONCAT('ID:', id, '(', CASE WHEN folder=1 THEN 'Inbox' WHEN folder=2 THEN 'Sent' ELSE CONCAT('Folder', folder) END, ')') SEPARATOR ', ') as instances
    FROM mail_mail 
    WHERE date_received >= DATE_SUB(NOW(), INTERVAL 2 HOUR)
    GROUP BY subject, from_text
    HAVING COUNT(*) > 1
    ORDER BY COUNT(*) DESC;
" 2>/dev/null

echo ""
echo "⚡ Service Status Summary:"
echo "========================="

# Check if services are processing emails simultaneously
recent_activity=$(mysql -h onlyoffice-mysql-server -u onlyoffice_user -ponlyoffice_pass onlyoffice -se "
    SELECT COUNT(*) FROM mail_mail WHERE date_received >= DATE_SUB(NOW(), INTERVAL 10 MINUTE);
" 2>/dev/null)

echo "📊 Recent email activity (10 min): $recent_activity emails"

if [ "$recent_activity" -gt 0 ]; then
    echo "⚠️  Multiple services may be processing emails simultaneously"
    echo "   This could cause the duplication and 'Ambiguous match found' errors"
fi

echo ""
echo "🔧 Recommended Actions:"
echo "1. Temporarily stop MailAggregator service to test if duplication stops"
echo "2. Check if our enhanced service works alone without conflicts"
echo "3. Consider updating MailAggregator to use enhanced DLL instead"