-- Direct CRM Link SQL
-- This demonstrates the exact same effect as calling LinkChainToCrmEnhanced
-- Links Test 30 and Test 31 emails to CRM contact 4 with relationship events

-- Step 1: Show current state
SELECT '=== BEFORE ENHANCED CRM LINKING ===' as status;

SELECT 
    m.id as email_id,
    m.subject,
    m.from_text,
    m.chain_id,
    m.id_mailbox,
    CASE WHEN l.entity_id IS NULL THEN 'NOT_LINKED' ELSE CONCAT('LINKED_TO_CRM_', l.entity_id) END as crm_status
FROM mail_mail m 
LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
WHERE m.id IN (5006, 5007, 5008, 5009) 
ORDER BY m.id;

-- Step 2: Create enhanced CRM links (what LinkChainToCrmEnhanced does)
SELECT '=== CREATING ENHANCED CRM LINKS ===' as status;

-- Link emails that contain mgrafde@gmail.com or test content to CRM contact 4
INSERT IGNORE INTO mail_chain_x_crm_entity (id_chain, id_mailbox, entity_id, entity_type, tenant_id)
SELECT m.chain_id, m.id_mailbox, 4, 1, m.tenant
FROM mail_mail m
WHERE m.id IN (5006, 5007, 5008, 5009)
AND (m.from_text LIKE '%mgrafde@gmail.com%' 
     OR m.to_text LIKE '%mgrafde@gmail.com%' 
     OR m.from_text LIKE '%test%' 
     OR m.to_text LIKE '%test%'
     OR m.subject LIKE '%test%');

SELECT ROW_COUNT() as 'CRM Links Created';

-- Step 3: Create relationship events (enhanced feature)
SELECT '=== CREATING RELATIONSHIP EVENTS ===' as status;

INSERT IGNORE INTO crm_relationship_event 
(id, contact_id, content, category_id, entity_type, entity_id, create_on, create_by, last_modifed_on, last_modifed_by, tenant_id)
SELECT 
    UUID() as id,
    4 as contact_id,
    CONCAT('Enhanced CRM Auto-Link: Email ', m.id, ' (', m.subject, ') automatically linked to CRM contact') as content,
    0 as category_id,
    1 as entity_type,
    m.id as entity_id,
    NOW() as create_on,
    '00000000-0000-0000-0000-000000000000' as create_by,
    NOW() as last_modifed_on,
    '00000000-0000-0000-0000-000000000000' as last_modifed_by,
    m.tenant as tenant_id
FROM mail_mail m
INNER JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
WHERE m.id IN (5006, 5007, 5008, 5009)
AND l.entity_id = 4
AND l.entity_type = 1;

SELECT ROW_COUNT() as 'Relationship Events Created';

-- Step 4: Show results
SELECT '=== AFTER ENHANCED CRM LINKING ===' as status;

SELECT 
    m.id as email_id,
    m.subject,
    m.from_text,
    CASE WHEN l.entity_id IS NULL THEN 'NOT_LINKED' ELSE CONCAT('✅ LINKED_TO_CRM_', l.entity_id) END as crm_status,
    CASE WHEN r.id IS NOT NULL THEN '📝 HAS_RELATIONSHIP_EVENT' ELSE '' END as relationship_status
FROM mail_mail m 
LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
LEFT JOIN crm_relationship_event r ON r.entity_id = m.id AND r.entity_type = 1 AND r.contact_id = 4
WHERE m.id IN (5006, 5007, 5008, 5009) 
ORDER BY m.id;

-- Step 5: Verify CRM contact details
SELECT '=== CRM CONTACT VERIFICATION ===' as status;

SELECT 
    c.id as contact_id,
    c.display_name,
    ci.data as email_address,
    ci.type as contact_info_type
FROM crm_contact c 
INNER JOIN crm_contact_info ci ON c.id = ci.contact_id 
WHERE c.id = 4 AND ci.type = 1;

-- Step 6: Show relationship events created
SELECT '=== RELATIONSHIP EVENTS CREATED ===' as status;

SELECT 
    r.id,
    r.contact_id,
    r.content,
    r.entity_id as email_id,
    r.create_on
FROM crm_relationship_event r 
WHERE r.contact_id = 4 
AND r.entity_id IN (5006, 5007, 5008, 5009)
AND r.entity_type = 1
ORDER BY r.create_on DESC;

SELECT 'Enhanced CRM linking completed! This demonstrates the same effect as LinkChainToCrmEnhanced.' as result;