#!/bin/bash

# Test script to temporarily use enhanced ASC.Mail.dll as ASC.Mail.Core.dll

echo "Backing up original ASC.Mail.Core.dll..."
cp /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Core.dll /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Core.dll.backup

echo "Replacing with enhanced version..."
cp /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Enhanced.dll /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Core.dll

echo "Testing MailAggregator startup..."
timeout 30 /usr/bin/dotnet /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Aggregator.Service.dll --help 2>&1

echo "Restoring original..."
cp /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Core.dll.backup /var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Core.dll

echo "Test complete."