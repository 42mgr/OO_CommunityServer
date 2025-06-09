-- Simple SQL-based validation of CRM monitoring logic
-- This simulates what the monitoring job should do

USE onlyoffice;

SELECT '=== CRM EMAIL MONITORING VALIDATION TEST ===' as test_step;

-- Step 1: Show test emails ready for processing
SELECT 'Step 1: Test emails ready for CRM processing' as test_step;
SELECT 
    m.id as email_id,
    m.from_text,
    m.to_text,
    m.subject,
    m.folder,
    CASE m.folder 
        WHEN 1 THEN 'Sent'
        WHEN 2 THEN 'Inbox'
        ELSE 'Other'
    END as folder_name
FROM mail_mail m
WHERE m.id >= 2001
  AND m.folder IN (1, 2)
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  )
ORDER BY m.date_received DESC;

-- Step 2: Show potential CRM matches (what the monitoring should find)
SELECT 'Step 2: CRM contacts that should be matched' as test_step;
SELECT 
    m.id as email_id,
    m.subject,
    SUBSTRING(m.from_text, 1, 60) as from_field,
    SUBSTRING(m.to_text, 1, 60) as to_field,
    c.id as contact_id,
    c.display_name as contact_name,
    ci.data as matching_email_address,
    'AUTO_MATCH_CANDIDATE' as match_type
FROM mail_mail m
CROSS JOIN crm_contact c
INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE ci.type = 1  -- Email type
  AND m.id >= 2001
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
ORDER BY m.id, c.id;

-- Step 3: Simulate the monitoring job by creating CRM relationship events
SELECT 'Step 3: Simulating CRM auto-linking (creating relationship events)' as test_step;

-- Create relationship events for email matches
INSERT INTO crm_relationship_event (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
SELECT DISTINCT
    c.id as contact_id,
    0 as entity_type,      -- 0 = Email
    m.id as entity_id,     -- Email ID
    CONCAT('AUTO_LINKED via ', ci.data) as content,
    NOW() as create_on,
    'system' as create_by,
    m.tenant as tenant_id,
    -3 as category_id,     -- -3 = Email category
    0 as have_files
FROM mail_mail m
CROSS JOIN crm_contact c
INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE ci.type = 1  -- Email type
  AND m.id >= 2001
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

-- Mark remaining emails as "no match"
INSERT INTO crm_relationship_event (contact_id, entity_type, entity_id, content, create_on, create_by, tenant_id, category_id, have_files)
SELECT DISTINCT
    0 as contact_id,       -- 0 = No contact match
    0 as entity_type,      -- 0 = Email
    m.id as entity_id,     -- Email ID
    'NO_CRM_MATCH' as content,
    NOW() as create_on,
    'system' as create_by,
    m.tenant as tenant_id,
    -99 as category_id,    -- -99 = No match category
    0 as have_files
FROM mail_mail m
WHERE m.id >= 2001
  AND m.folder IN (1, 2)
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  );

-- Step 4: Show results of the simulated monitoring
SELECT 'Step 4: Results of CRM auto-linking simulation' as test_step;

SELECT 
    cre.entity_id as email_id,
    m.subject,
    CASE 
        WHEN cre.contact_id = 0 THEN 'NO MATCH'
        ELSE c.display_name
    END as linked_contact,
    cre.content as link_details,
    cre.create_on as linked_at,
    CASE cre.category_id
        WHEN -3 THEN 'CRM_LINKED'
        WHEN -99 THEN 'NO_MATCH'
        ELSE 'OTHER'
    END as result_type
FROM crm_relationship_event cre
LEFT JOIN mail_mail m ON cre.entity_id = m.id
LEFT JOIN crm_contact c ON cre.contact_id = c.id
WHERE cre.entity_type = 0  -- Email events
  AND cre.entity_id >= 2001
ORDER BY cre.entity_id, cre.contact_id;

-- Step 5: Summary statistics
SELECT 'Step 5: Summary Statistics' as test_step;

SELECT 
    'Test Emails Created' as metric,
    COUNT(*) as count
FROM mail_mail 
WHERE id >= 2001
UNION ALL
SELECT 
    'Emails with CRM Links',
    COUNT(DISTINCT cre.entity_id)
FROM crm_relationship_event cre 
WHERE cre.entity_type = 0 AND cre.entity_id >= 2001 AND cre.category_id = -3
UNION ALL
SELECT 
    'Emails with No Match',
    COUNT(DISTINCT cre.entity_id)
FROM crm_relationship_event cre 
WHERE cre.entity_type = 0 AND cre.entity_id >= 2001 AND cre.category_id = -99
UNION ALL
SELECT 
    'Total CRM Events Created',
    COUNT(*)
FROM crm_relationship_event cre 
WHERE cre.entity_type = 0 AND cre.entity_id >= 2001;

SELECT '=== VALIDATION TEST COMPLETE ===' as test_step;

-- Final verification: Check if any emails are still unprocessed
SELECT 
    CASE 
        WHEN COUNT(*) = 0 THEN '✅ SUCCESS: All test emails have been processed for CRM linking'
        ELSE CONCAT('⚠️  WARNING: ', COUNT(*), ' test emails still unprocessed')
    END as final_result
FROM mail_mail m
WHERE m.id >= 2001
  AND m.folder IN (1, 2)
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  );