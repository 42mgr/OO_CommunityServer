-- Manual function to link specific unlinked emails to CRM contacts
-- This will process emails that SHOULD_AUTO_LINK based on our analysis

-- Function to manually link emails that should have been auto-linked
INSERT INTO crm_relationship_event (
    tenant_id, 
    contact_id, 
    content, 
    create_on, 
    create_by, 
    entity_type, 
    entity_id, 
    category_id, 
    last_modifed_on, 
    last_modifed_by, 
    have_files
)
SELECT 
    m.tenant,
    c.id as contact_id,
    CONCAT(
        '{"from":"', REPLACE(REPLACE(m.from_text, '\\', '\\\\'), '"', '\\"'),
        '","to":"', REPLACE(REPLACE(IFNULL(m.to_text, ''), '\\', '\\\\'), '"', '\\"'),
        '","cc":"', REPLACE(REPLACE(IFNULL(m.cc, ''), '\\', '\\\\'), '"', '\\"'),
        '","bcc":"","subject":"', REPLACE(REPLACE(IFNULL(m.subject, ''), '\\', '\\\\'), '"', '\\"'),
        '","important":', IF(m.importance, 'true', 'false'),
        ',"chain_id":"', IFNULL(m.chain_id, ''),
        '","is_sended":false,"date_created":"', DATE_FORMAT(m.date_received, '%m/%d/%Y %H:%i:%s'),
        '","introduction":"', REPLACE(REPLACE(IFNULL(m.introduction, ''), '\\', '\\\\'), '"', '\\"'),
        '","message_id":', m.id,
        ',"message_url":"/Products/CRM/HttpHandlers/filehandler.ashx?action=mailmessage&message_id=', m.id, '"}'
    ) as json_content,
    m.date_received,
    m.id_user,
    0,  -- Entity type for email
    m.id,
    -3, -- Email category  
    NOW(),
    m.id_user,
    0   -- No files initially
FROM mail_mail m
JOIN crm_contact c ON c.tenant_id = m.tenant
JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE m.tenant = 1 
  AND m.folder = 2  -- Only inbox emails
  AND ci.type = 1  -- Email type
  AND ci.data = TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1))
  AND m.id IN (
      -- Specific emails that should be auto-linked based on our analysis
      326, 318, 312, 313, 304, 301, 298, 291, 289, 287, 284, 282, 280, 278, 276, 272, 258, 228
  )
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  );

-- Show results of manual linking
SELECT 
    'MANUAL LINKING RESULTS' as report_type,
    m.id as email_id,
    m.subject,
    m.from_text,
    CASE 
        WHEN EXISTS (SELECT 1 FROM crm_relationship_event cre WHERE cre.entity_type = 0 AND cre.entity_id = m.id)
        THEN 'NOW LINKED TO CRM'
        ELSE 'STILL NOT LINKED'
    END as new_status,
    cre.contact_id,
    CONCAT(c.first_name, ' ', c.last_name) as linked_contact_name
FROM mail_mail m
LEFT JOIN crm_relationship_event cre ON (cre.entity_type = 0 AND cre.entity_id = m.id)
LEFT JOIN crm_contact c ON cre.contact_id = c.id
WHERE m.id IN (326, 318, 312, 313, 304, 301, 298, 291, 289, 287, 284, 282, 280, 278, 276, 272, 258, 228)
ORDER BY m.id DESC;

-- Summary of total CRM-linked emails now
SELECT 
    'FINAL SUMMARY' as report_type,
    COUNT(*) as total_crm_linked_emails,
    COUNT(DISTINCT cre.contact_id) as contacts_with_emails,
    MIN(cre.create_on) as first_event,
    MAX(cre.create_on) as last_event
FROM crm_relationship_event cre
WHERE cre.entity_type = 0  -- Email events
  AND cre.entity_id >= 200;  -- Recent emails