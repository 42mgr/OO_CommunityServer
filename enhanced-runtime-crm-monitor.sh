#!/bin/bash

# Enhanced Runtime CRM Email Monitor - Full Integration Version
# This version triggers the complete CRM integration workflow

echo "🚀 ONLYOFFICE CRM Email Monitor - Enhanced Edition"
echo "================================================="

MYSQL_CMD="mysql -h onlyoffice-mysql-server -u onlyoffice_user -ponlyoffice_pass onlyoffice"
LAST_CHECK=$(date '+%Y-%m-%d %H:%M:%S')

echo "[CRM-MONITOR] ✅ Starting enhanced monitoring - full CRM integration"
echo "[CRM-MONITOR] 📊 Last check time: $LAST_CHECK"

monitor_emails() {
    local current_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[CRM-MONITOR] 🔍 Checking for new emails since $LAST_CHECK"
    
    # Find unprocessed emails with full details
    local unprocessed_emails=$($MYSQL_CMD -se "
        SELECT m.id, m.tenant, m.id_user, m.id_mailbox, m.chain_id, m.from_text, m.to_text, m.cc, m.subject, m.folder
        FROM mail_mail m
        WHERE m.date_received > '$LAST_CHECK'
        AND m.folder IN (1, 2)
        AND NOT EXISTS (
            SELECT 1 FROM crm_relationship_event cre 
            WHERE cre.entity_type = 0 AND cre.entity_id = m.id
        )
        ORDER BY m.date_received ASC;" 2>/dev/null)
    
    if [ -n "$unprocessed_emails" ]; then
        local email_count=$(echo "$unprocessed_emails" | wc -l)
        echo "[CRM-MONITOR] 📧 Found $email_count unprocessed emails"
        
        # Process each email individually for full CRM integration
        echo "$unprocessed_emails" | while IFS=$'\t' read -r email_id tenant user_id mailbox_id chain_id from_text to_text cc subject folder; do
            echo "[CRM-MONITOR] 🔧 Processing email $email_id: $subject"
            
            # Find matching CRM contacts for this email
            local matches=$($MYSQL_CMD -se "
                SELECT DISTINCT c.id, c.display_name, ci.data
                FROM crm_contact c
                INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
                WHERE c.tenant_id = $tenant
                AND ci.type = 1
                AND (
                    LOWER('$from_text') LIKE CONCAT('%', LOWER(ci.data), '%')
                    OR LOWER('$to_text') LIKE CONCAT('%', LOWER(ci.data), '%')
                    OR LOWER('$cc') LIKE CONCAT('%', LOWER(ci.data), '%')
                );" 2>/dev/null)
            
            if [ -n "$matches" ]; then
                local match_count=$(echo "$matches" | wc -l)
                echo "[CRM-MONITOR] 🎯 Found $match_count CRM matches for email $email_id"
                
                # Process each match with full CRM integration
                echo "$matches" | while IFS=$'\t' read -r contact_id contact_name matching_email; do
                    echo "[CRM-MONITOR] 🔗 Linking email $email_id to $contact_name via $matching_email"
                    
                    # Full CRM integration workflow
                    $MYSQL_CMD -se "
                        -- 1. Create CRM relationship event (email history)
                        INSERT IGNORE INTO crm_relationship_event 
                        (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
                        VALUES (
                            $contact_id, 
                            0, 
                            $email_id, 
                            JSON_OBJECT(
                                'from', '$from_text',
                                'to', '$to_text', 
                                'subject', '$subject',
                                'folder', $folder,
                                'auto_linked', 'true',
                                'matched_email', '$matching_email'
                            ),
                            NOW(), 
                            'enhanced-monitor', 
                            $tenant, 
                            -3, 
                            0
                        );
                        
                        -- 2. Link email chain to CRM entity (if chain exists)
                        INSERT IGNORE INTO mail_chain_x_crm_entity 
                        (id_tenant, id_mailbox, id_chain, entity_id, entity_type)
                        SELECT $tenant, $mailbox_id, '$chain_id', $contact_id, 0
                        WHERE '$chain_id' IS NOT NULL AND '$chain_id' != '';
                        
                        -- 3. Update chain CRM status
                        UPDATE mail_chain 
                        SET is_crm_chain = 1 
                        WHERE id = '$chain_id' 
                        AND id_mailbox = $mailbox_id 
                        AND tenant = $tenant
                        AND '$chain_id' IS NOT NULL AND '$chain_id' != '';
                        
                        -- 4. Update email CRM flag
                        UPDATE mail_mail 
                        SET is_from_crm = 1
                        WHERE id = $email_id;
                        
                        -- 5. Create CRM history entry for visibility
                        INSERT IGNORE INTO crm_entity_contact 
                        (entity_id, entity_type, contact_id, tenant_id)
                        VALUES ($email_id, 2, $contact_id, $tenant)
                        ON DUPLICATE KEY UPDATE contact_id = $contact_id;
                    " 2>/dev/null
                    
                    echo "[CRM-MONITOR] ✅ Full CRM integration completed for email $email_id → $contact_name"
                done
                
                # Trigger CRM cache refresh (if needed)
                $MYSQL_CMD -se "
                    -- Update contact last activity
                    UPDATE crm_contact 
                    SET last_modifed_on = NOW() 
                    WHERE id IN (
                        SELECT DISTINCT contact_id 
                        FROM crm_relationship_event 
                        WHERE entity_id = $email_id AND entity_type = 0
                    );
                " 2>/dev/null
                
            else
                echo "[CRM-MONITOR] 📭 No CRM matches for email $email_id"
                
                # Mark as processed with no match
                $MYSQL_CMD -se "
                    INSERT INTO crm_relationship_event 
                    (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
                    VALUES (0, 0, $email_id, 'NO_CRM_MATCH', NOW(), 'enhanced-monitor', $tenant, -99, 0);
                " 2>/dev/null
            fi
        done
        
        echo "[CRM-MONITOR] ✅ Enhanced processing completed for $email_count emails"
        
        # Show summary of what was processed
        $MYSQL_CMD -se "
            SELECT 
                CONCAT('[CRM-MONITOR] 📊 Email ', cre.entity_id, ' (', m.subject, ') → ', 
                       COALESCE(c.display_name, 'NO MATCH'), 
                       CASE WHEN mc.is_crm_chain = 1 THEN ' [CHAIN LINKED]' ELSE '' END) as summary
            FROM crm_relationship_event cre
            LEFT JOIN mail_mail m ON cre.entity_id = m.id
            LEFT JOIN crm_contact c ON cre.contact_id = c.id
            LEFT JOIN mail_chain mc ON m.chain_id = mc.id AND m.id_mailbox = mc.id_mailbox
            WHERE cre.entity_type = 0 
              AND cre.create_by = 'enhanced-monitor'
              AND cre.create_on > '$LAST_CHECK'
            ORDER BY cre.entity_id;
        " 2>/dev/null
        
    fi
    
    LAST_CHECK="$current_time"
}

# Signal handler for graceful shutdown
cleanup() {
    echo "[CRM-MONITOR] 🛑 Stopping enhanced monitoring..."
    exit 0
}

trap cleanup SIGTERM SIGINT

# Main monitoring loop
while true; do
    monitor_emails
    sleep 30
done