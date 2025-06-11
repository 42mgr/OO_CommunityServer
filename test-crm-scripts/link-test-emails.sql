-- Link Test 30 and Test 31 emails to CRM contact 4
-- This demonstrates the core CRM email linking functionality

-- First, let's see the current state
SELECT 'Current state of Test 30 and Test 31 emails:' as info;
SELECT 
    m.id, 
    m.subject, 
    m.from_text, 
    m.chain_id,
    m.id_mailbox,
    CASE WHEN l.entity_id IS NULL THEN 'NOT_LINKED' ELSE CONCAT('LINKED_TO_', l.entity_id) END as status
FROM mail_mail m 
LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
WHERE m.id IN (5006, 5007, 5008, 5009) 
ORDER BY m.id;

-- Now create the CRM links for emails that contain mgrafde@gmail.com
SELECT 'Creating CRM links for Test 30 and Test 31 emails...' as info;

INSERT IGNORE INTO mail_chain_x_crm_entity (id_chain, id_mailbox, entity_id, entity_type)
SELECT m.chain_id, m.id_mailbox, 4, 1
FROM mail_mail m
WHERE m.id IN (5006, 5007, 5008, 5009)
AND (m.from_text LIKE '%mgrafde@gmail.com%' OR m.to_text LIKE '%mgrafde@gmail.com%');

-- Check the results
SELECT 'Results after linking:' as info;
SELECT 
    m.id, 
    m.subject, 
    m.from_text, 
    CASE WHEN l.entity_id IS NULL THEN 'NOT_LINKED' ELSE CONCAT('LINKED_TO_', l.entity_id) END as status
FROM mail_mail m 
LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
WHERE m.id IN (5006, 5007, 5008, 5009) 
ORDER BY m.id;

-- Verify the CRM contact details
SELECT 'CRM Contact 4 details:' as info;
SELECT c.id, c.display_name, ci.data as email 
FROM crm_contact c 
INNER JOIN crm_contact_info ci ON c.id = ci.contact_id 
WHERE c.id = 4 AND ci.type = 1;