-- Function to analyze unlinked emails and match them with CRM contacts
-- This will help us understand what emails should be linked and why they aren't

-- Step 1: Show all unlinked inbox emails from recent time
SELECT 
    'UNLINKED EMAILS' as analysis_type,
    m.id as email_id,
    m.subject,
    m.from_text,
    m.date_received,
    m.folder,
    CASE 
        WHEN m.folder = 1 THEN 'Sent'
        WHEN m.folder = 2 THEN 'Inbox' 
        WHEN m.folder = 3 THEN 'Spam'
        ELSE CONCAT('Folder_', m.folder)
    END as folder_name
FROM mail_mail m
WHERE m.tenant = 1 
  AND m.date_received > '2025-06-09 09:00:00'
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  )
ORDER BY m.date_received DESC;

-- Step 2: Show all CRM contacts with their email addresses
SELECT 
    'CRM CONTACTS' as analysis_type,
    c.id as contact_id,
    CONCAT(c.first_name, ' ', c.last_name) as contact_name,
    c.display_name,
    ci.data as email_address,
    ci.category as email_category
FROM crm_contact c
JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE c.tenant_id = 1 
  AND ci.type = 1  -- Email type
ORDER BY c.id;

-- Step 3: Email address extraction test - see how our extraction logic works
SELECT 
    'EMAIL EXTRACTION TEST' as analysis_type,
    m.id as email_id,
    m.from_text as original_from,
    TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)) as extracted_email,
    CASE 
        WHEN TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)) = m.from_text 
        THEN 'No brackets - using full text'
        ELSE 'Extracted from brackets'
    END as extraction_method
FROM mail_mail m
WHERE m.tenant = 1 
  AND m.date_received > '2025-06-09 09:00:00'
ORDER BY m.date_received DESC;

-- Step 4: Potential matches - which emails SHOULD be linked to which contacts
SELECT 
    'POTENTIAL MATCHES' as analysis_type,
    m.id as email_id,
    m.subject,
    m.from_text,
    TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)) as extracted_email,
    c.id as contact_id,
    CONCAT(c.first_name, ' ', c.last_name) as contact_name,
    ci.data as contact_email,
    CASE 
        WHEN TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)) = ci.data 
        THEN 'EXACT MATCH'
        ELSE 'NO MATCH'
    END as match_status,
    EXISTS (
        SELECT 1 FROM crm_relationship_event cre 
        WHERE cre.entity_type = 0 AND cre.entity_id = m.id
    ) as already_linked
FROM mail_mail m
CROSS JOIN crm_contact c
JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE m.tenant = 1 
  AND c.tenant_id = 1
  AND ci.type = 1  -- Email type
  AND m.date_received > '2025-06-09 09:00:00'
  AND TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)) = ci.data
ORDER BY m.date_received DESC, c.id;

-- Step 5: Check existing CRM events to see the format
SELECT 
    'EXISTING CRM EVENTS' as analysis_type,
    cre.id as event_id,
    cre.entity_id as email_id,
    cre.contact_id,
    cre.create_on,
    LEFT(cre.content, 100) as content_preview,
    cre.category_id,
    cre.have_files
FROM crm_relationship_event cre
WHERE cre.entity_type = 0  -- Email events
  AND cre.create_on > '2025-06-09 09:00:00'
ORDER BY cre.create_on DESC;

-- Step 6: Check why contact history might not be loading - validate JSON format
SELECT 
    'JSON VALIDATION' as analysis_type,
    cre.entity_id as email_id,
    cre.contact_id,
    cre.content,
    CASE 
        WHEN JSON_VALID(cre.content) THEN 'VALID JSON'
        ELSE 'INVALID JSON'
    END as json_status
FROM crm_relationship_event cre
WHERE cre.entity_type = 0  -- Email events
  AND cre.entity_id IN (336, 338, 341)  -- Our test emails
ORDER BY cre.entity_id;