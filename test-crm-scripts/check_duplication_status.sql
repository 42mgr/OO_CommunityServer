-- Check for email duplication issues in the current running system
-- This script identifies if there are duplicate CRM relationship events

-- 1. Check for duplicate CRM relationship events (the main duplication issue)
SELECT 
    'DUPLICATE_CRM_EVENTS' as issue_type,
    cre.entity_id as email_id,
    cre.contact_id,
    COUNT(*) as duplicate_count,
    GROUP_CONCAT(cre.id ORDER BY cre.id) as event_ids,
    m.subject,
    m.from_text
FROM crm_relationship_event cre
JOIN mail_mail m ON cre.entity_id = m.id
WHERE cre.entity_type = 0  -- Email events
GROUP BY cre.entity_id, cre.contact_id
HAVING COUNT(*) > 1
ORDER BY duplicate_count DESC, cre.entity_id DESC;

-- 2. Check recent emails for potential duplication pattern
SELECT 
    'RECENT_EMAIL_CRM_STATUS' as issue_type,
    m.id as email_id,
    m.subject,
    m.from_text,
    m.date_received,
    COUNT(cre.id) as crm_link_count,
    GROUP_CONCAT(cre.contact_id) as linked_contact_ids
FROM mail_mail m
LEFT JOIN crm_relationship_event cre ON (cre.entity_type = 0 AND cre.entity_id = m.id)
WHERE m.date_received >= DATE_SUB(NOW(), INTERVAL 2 HOUR)
GROUP BY m.id
ORDER BY m.date_received DESC;

-- 3. Summary of duplication status
SELECT 
    'DUPLICATION_SUMMARY' as issue_type,
    COUNT(DISTINCT cre.entity_id) as total_emails_with_crm_links,
    COUNT(cre.id) as total_crm_relationship_events,
    COUNT(cre.id) - COUNT(DISTINCT cre.entity_id) as excess_events,
    CASE 
        WHEN COUNT(cre.id) - COUNT(DISTINCT cre.entity_id) > 0 THEN 'DUPLICATION_DETECTED'
        ELSE 'NO_DUPLICATION'
    END as status
FROM crm_relationship_event cre
WHERE cre.entity_type = 0;

-- 4. Check if WebCrmMonitoringService is mentioned in recent activity
-- This gives us a hint if our service is actually running
SELECT 
    'SERVICE_ACTIVITY_CHECK' as issue_type,
    'Checking for recent CRM auto-linking activity...' as description,
    COUNT(*) as recent_crm_events_last_hour
FROM crm_relationship_event cre
WHERE cre.entity_type = 0 
  AND cre.create_on >= DATE_SUB(NOW(), INTERVAL 1 HOUR);