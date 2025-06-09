#!/bin/bash

# Runtime CRM Email Monitor - Shell Script Version
# This version uses direct MySQL commands for maximum compatibility

echo "🚀 ONLYOFFICE CRM Email Monitor - Shell Edition"
echo "=============================================="

MYSQL_CMD="mysql -h onlyoffice-mysql-server -u onlyoffice_user -ponlyoffice_pass onlyoffice"
LAST_CHECK=$(date '+%Y-%m-%d %H:%M:%S')

echo "[CRM-MONITOR] ✅ Starting monitoring - checking every 30 seconds"
echo "[CRM-MONITOR] 📊 Last check time: $LAST_CHECK"

monitor_emails() {
    local current_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[CRM-MONITOR] 🔍 Checking for new emails since $LAST_CHECK"
    
    # Find unprocessed emails
    local unprocessed_count=$($MYSQL_CMD -se "
        SELECT COUNT(*) FROM mail_mail m
        WHERE m.date_received > '$LAST_CHECK'
        AND m.folder IN (1, 2)
        AND NOT EXISTS (
            SELECT 1 FROM crm_relationship_event cre 
            WHERE cre.entity_type = 0 AND cre.entity_id = m.id
        );" 2>/dev/null)
    
    if [ "$unprocessed_count" -gt 0 ]; then
        echo "[CRM-MONITOR] 📧 Found $unprocessed_count unprocessed emails"
        
        # Process CRM linking using SQL
        local processed=$($MYSQL_CMD -se "
            -- Create CRM relationship events for email matches
            INSERT INTO crm_relationship_event (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
            SELECT DISTINCT
                c.id as contact_id,
                0 as entity_type,
                m.id as entity_id,
                CONCAT('SHELL_AUTO_LINKED via ', ci.data) as content,
                NOW() as create_on,
                'crm-monitor' as create_by,
                m.tenant as tenant_id,
                -3 as category_id,
                0 as have_files
            FROM mail_mail m
            CROSS JOIN crm_contact c
            INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
            WHERE ci.type = 1
              AND m.date_received > '$LAST_CHECK'
              AND m.folder IN (1, 2)
              AND (
                  LOWER(m.from_text) LIKE CONCAT('%', LOWER(ci.data), '%')
                  OR LOWER(m.to_text) LIKE CONCAT('%', LOWER(ci.data), '%')
                  OR LOWER(m.cc) LIKE CONCAT('%', LOWER(ci.data), '%')
              )
              AND NOT EXISTS (
                  SELECT 1 FROM crm_relationship_event cre 
                  WHERE cre.entity_type = 0 AND cre.entity_id = m.id AND cre.contact_id = c.id
              );
            
            -- Get count of processed emails
            SELECT ROW_COUNT();
        " 2>/dev/null | tail -1)
        
        # Mark remaining emails as no-match
        $MYSQL_CMD -se "
            INSERT INTO crm_relationship_event (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
            SELECT DISTINCT
                0 as contact_id,
                0 as entity_type,
                m.id as entity_id,
                'NO_CRM_MATCH' as content,
                NOW() as create_on,
                'crm-monitor' as create_by,
                m.tenant as tenant_id,
                -99 as category_id,
                0 as have_files
            FROM mail_mail m
            WHERE m.date_received > '$LAST_CHECK'
              AND m.folder IN (1, 2)
              AND NOT EXISTS (
                  SELECT 1 FROM crm_relationship_event cre 
                  WHERE cre.entity_type = 0 AND cre.entity_id = m.id
              );
        " 2>/dev/null
        
        echo "[CRM-MONITOR] ✅ Processed $unprocessed_count emails, $processed CRM links created"
        
        # Show what was linked
        $MYSQL_CMD -se "
            SELECT 
                CONCAT('[CRM-MONITOR] 🎯 Email ', cre.entity_id, ': ', m.subject, ' → ', 
                       COALESCE(c.display_name, 'NO MATCH'), ' (', cre.content, ')') as result
            FROM crm_relationship_event cre
            LEFT JOIN mail_mail m ON cre.entity_id = m.id
            LEFT JOIN crm_contact c ON cre.contact_id = c.id
            WHERE cre.entity_type = 0 
              AND cre.create_by = 'crm-monitor'
              AND cre.create_on > '$LAST_CHECK'
            ORDER BY cre.entity_id;
        " 2>/dev/null
    fi
    
    LAST_CHECK="$current_time"
}

# Signal handler for graceful shutdown
cleanup() {
    echo "[CRM-MONITOR] 🛑 Stopping monitoring..."
    exit 0
}

trap cleanup SIGTERM SIGINT

# Main monitoring loop
while true; do
    monitor_emails
    sleep 30
done