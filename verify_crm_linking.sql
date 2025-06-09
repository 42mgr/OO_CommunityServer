-- Comprehensive verification of CRM auto-linking functionality

-- Check all test emails and their CRM linking status
SELECT 
    'FINAL STATUS CHECK' as report_type,
    m.id as email_id,
    m.subject,
    m.from_text,
    CASE 
        WHEN m.folder = 1 THEN 'Sent'
        WHEN m.folder = 2 THEN 'Inbox' 
        ELSE CONCAT('Folder_', m.folder)
    END as folder_name,
    m.date_received,
    CASE 
        WHEN EXISTS (SELECT 1 FROM crm_relationship_event cre WHERE cre.entity_type = 0 AND cre.entity_id = m.id)
        THEN 'LINKED TO CRM'
        ELSE 'NOT LINKED'
    END as crm_status,
    cre.contact_id,
    CONCAT(c.first_name, ' ', c.last_name) as linked_contact_name
FROM mail_mail m
LEFT JOIN crm_relationship_event cre ON (cre.entity_type = 0 AND cre.entity_id = m.id)
LEFT JOIN crm_contact c ON cre.contact_id = c.id
WHERE m.subject LIKE 'Test %' 
  AND m.date_received > '2025-06-09 09:00:00'
ORDER BY m.id;

-- Check JSON validity of all CRM events
SELECT 
    'JSON STATUS' as report_type,
    cre.entity_id as email_id,
    cre.contact_id,
    CASE 
        WHEN JSON_VALID(cre.content) THEN 'VALID JSON'
        ELSE 'INVALID JSON'
    END as json_status
FROM crm_relationship_event cre
WHERE cre.entity_type = 0
  AND cre.entity_id >= 329  -- All our test emails
ORDER BY cre.entity_id;

-- Summary statistics
SELECT 
    'SUMMARY STATS' as report_type,
    COUNT(*) as total_test_emails,
    SUM(CASE WHEN EXISTS (SELECT 1 FROM crm_relationship_event cre WHERE cre.entity_type = 0 AND cre.entity_id = m.id) THEN 1 ELSE 0 END) as linked_emails,
    SUM(CASE WHEN m.folder = 2 THEN 1 ELSE 0 END) as inbox_emails,
    SUM(CASE WHEN m.folder = 1 THEN 1 ELSE 0 END) as sent_emails
FROM mail_mail m
WHERE m.subject LIKE 'Test %' 
  AND m.date_received > '2025-06-09 09:00:00';