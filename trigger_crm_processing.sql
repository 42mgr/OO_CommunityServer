-- SQL script to manually trigger CRM processing for recent unlinked emails
-- This simulates what the enhanced auto-linking should do

-- First, let's see what emails need CRM processing
SELECT 
    m.id,
    m.subject,
    m.from_text,
    m.to_text,
    m.date_received,
    'No CRM Link' as status
FROM mail_mail m
WHERE m.tenant = 1 
  AND m.folder = 2  -- Inbox folder
  AND m.date_received > DATE_SUB(NOW(), INTERVAL 2 HOURS)
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  )
ORDER BY m.date_received DESC;

-- Check if we have CRM contacts that match the email addresses
SELECT DISTINCT
    m.id as email_id,
    m.subject,
    m.from_text,
    c.id as contact_id,
    CONCAT(c.first_name, ' ', c.last_name) as contact_name
FROM mail_mail m
JOIN crm_contact c ON (
    LOWER(c.first_name) LIKE CONCAT('%', LOWER(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)), '%')
    OR LOWER(c.last_name) LIKE CONCAT('%', LOWER(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)), '%')
    OR EXISTS (
        SELECT 1 FROM crm_contact_info ci 
        WHERE ci.contact_id = c.id 
        AND ci.data = SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)
    )
)
WHERE m.tenant = 1 
  AND m.folder = 2  -- Inbox folder
  AND m.date_received > DATE_SUB(NOW(), INTERVAL 2 HOURS)
  AND c.tenant_id = 1
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  )
ORDER BY m.date_received DESC;