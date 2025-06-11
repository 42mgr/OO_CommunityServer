-- Validation script for CRM Email Monitoring
-- Run this to check if the monitoring is working correctly

USE onlyoffice;

SELECT '=== CRM EMAIL MONITORING VALIDATION ===' as validation_check;

-- 1. Check for unprocessed emails (should find emails ready for processing)
SELECT 'Step 1: Unprocessed Emails' as check_name;
SELECT 
    m.id as email_id,
    m.from_text,
    m.to_text,
    m.subject,
    m.date_received,
    m.folder,
    CASE m.folder 
        WHEN 1 THEN 'Sent'
        WHEN 2 THEN 'Inbox'
        ELSE 'Other'
    END as folder_name
FROM mail_mail m
WHERE m.folder IN (1, 2)  -- 1=Sent, 2=Inbox
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  )
ORDER BY m.date_received DESC
LIMIT 10;

-- 2. Check CRM contacts with email addresses
SELECT 'Step 2: CRM Contacts with Email Addresses' as check_name;
SELECT 
    c.id as contact_id,
    c.display_name,
    ci.data as email_address,
    CASE c.is_company 
        WHEN 1 THEN 'Company'
        ELSE 'Person'
    END as contact_type
FROM crm_contact c
INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE ci.type = 1  -- Email type
ORDER BY c.display_name
LIMIT 10;

-- 3. Test email address extraction (simulating the monitoring logic)
SELECT 'Step 3: Email Address Extraction Test' as check_name;
SELECT 
    m.id as email_id,
    m.from_text,
    -- Extract email from from_text using basic pattern matching
    SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1) as extracted_from_email,
    m.to_text,
    -- Simple extraction for to_text (assuming single email)
    CASE 
        WHEN m.to_text LIKE '%<%' THEN SUBSTRING_INDEX(SUBSTRING_INDEX(m.to_text, '<', -1), '>', 1)
        ELSE m.to_text
    END as extracted_to_email
FROM mail_mail m
WHERE m.folder IN (1, 2)
ORDER BY m.date_received DESC
LIMIT 5;

-- 4. Find potential matches (emails that could be linked to CRM)
SELECT 'Step 4: Potential CRM Matches' as check_name;
SELECT 
    m.id as email_id,
    m.subject,
    m.from_text,
    c.id as contact_id,
    c.display_name as contact_name,
    ci.data as matching_email,
    'FROM field match' as match_type
FROM mail_mail m
CROSS JOIN crm_contact c
INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE ci.type = 1  -- Email type
  AND m.folder IN (1, 2)
  AND (
      LOWER(m.from_text) LIKE CONCAT('%', LOWER(ci.data), '%')
      OR LOWER(m.to_text) LIKE CONCAT('%', LOWER(ci.data), '%')
      OR LOWER(m.cc) LIKE CONCAT('%', LOWER(ci.data), '%')
  )
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  )
ORDER BY m.date_received DESC
LIMIT 10;

-- 5. Check existing CRM relationships (already processed emails)
SELECT 'Step 5: Existing CRM Relationships' as check_name;
SELECT 
    cre.id as event_id,
    cre.entity_id as email_id,
    c.display_name as contact_name,
    cre.create_on as linked_date,
    cre.content,
    CASE cre.category_id
        WHEN -3 THEN 'Email Event'
        WHEN -99 THEN 'No Match'
        ELSE 'Other'
    END as event_category
FROM crm_relationship_event cre
LEFT JOIN crm_contact c ON cre.contact_id = c.id
WHERE cre.entity_type = 0  -- Email entity type
ORDER BY cre.create_on DESC
LIMIT 10;

-- 6. Check mail chain CRM linkings
SELECT 'Step 6: Mail Chain CRM Links' as check_name;
SELECT 
    mcx.id_chain as chain_id,
    mcx.entity_id as contact_id,
    c.display_name as contact_name,
    mc.length as emails_in_chain,
    mc.is_crm_chain
FROM mail_chain_x_crm_entity mcx
LEFT JOIN crm_contact c ON mcx.entity_id = c.id
LEFT JOIN mail_chain mc ON mcx.id_chain = mc.id AND mcx.id_mailbox = mc.id_mailbox
ORDER BY mcx.id_chain
LIMIT 10;

-- 7. Summary statistics
SELECT 'Step 7: Summary Statistics' as check_name;
SELECT 
    (SELECT COUNT(*) FROM mail_mail WHERE folder IN (1,2)) as total_emails,
    (SELECT COUNT(*) FROM crm_contact) as total_crm_contacts,
    (SELECT COUNT(DISTINCT ci.contact_id) FROM crm_contact_info ci WHERE ci.type = 1) as contacts_with_email,
    (SELECT COUNT(*) FROM mail_mail m WHERE m.folder IN (1,2) AND NOT EXISTS (SELECT 1 FROM crm_relationship_event cre WHERE cre.entity_type = 0 AND cre.entity_id = m.id)) as unprocessed_emails,
    (SELECT COUNT(*) FROM crm_relationship_event WHERE entity_type = 0) as emails_with_crm_events,
    (SELECT COUNT(*) FROM mail_chain_x_crm_entity) as chain_crm_links;

-- 8. Recent activity (emails from last hour)
SELECT 'Step 8: Recent Email Activity' as check_name;
SELECT 
    COUNT(*) as recent_emails,
    MIN(date_received) as earliest_recent,
    MAX(date_received) as latest_recent
FROM mail_mail 
WHERE date_received > DATE_SUB(NOW(), INTERVAL 1 HOUR)
  AND folder IN (1, 2);

-- 9. Database indexes check (performance)
SELECT 'Step 9: Database Indexes (for performance)' as check_name;
SHOW INDEX FROM mail_mail WHERE Key_name LIKE '%date%' OR Key_name LIKE '%folder%';

SELECT 'VALIDATION COMPLETE' as validation_status;
SELECT 'If unprocessed_emails > 0 and contacts_with_email > 0, monitoring should find matches' as note;