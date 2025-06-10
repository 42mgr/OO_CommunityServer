-- Diagnostic SQL for CRM Auto-Linking Service
-- Run this to troubleshoot why emails aren't being auto-linked

-- 1. Check recent emails (last 24 hours)
SELECT 'Recent Emails (last 24h)' as check_name;
SELECT 
    id,
    date_received,
    from_text,
    to_text,
    subject,
    folder,
    tenant
FROM mail_mail 
WHERE date_received >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
ORDER BY date_received DESC 
LIMIT 10;

-- 2. Check if any emails are linked to CRM
SELECT 'Linked Emails to CRM' as check_name;
SELECT 
    m.id as email_id,
    m.subject,
    m.from_text,
    m.date_received,
    l.entity_id as crm_contact_id,
    l.entity_type
FROM mail_mail m
INNER JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
WHERE m.date_received >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
ORDER BY m.date_received DESC
LIMIT 10;

-- 3. Check CRM contacts with emails
SELECT 'CRM Contacts with Emails' as check_name;
SELECT 
    c.id,
    c.display_name,
    ci.data as email_address,
    c.tenant_id
FROM crm_contact c
INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE ci.type = 1 -- Email type
AND c.status_id != 1 -- Not deleted
ORDER BY c.id DESC
LIMIT 10;

-- 4. Check if test email matches any CRM contact
SELECT 'Test Email Match Check' as check_name;
SELECT 
    m.id as email_id,
    m.subject,
    m.from_text,
    m.to_text,
    'Potential CRM matches:' as match_info
FROM mail_mail m
WHERE m.date_received >= DATE_SUB(NOW(), INTERVAL 2 HOUR)
ORDER BY m.date_received DESC
LIMIT 5;

-- Check for matching CRM contacts for recent emails
SELECT 'CRM Contact Matches for Recent Emails' as check_name;
SELECT DISTINCT
    m.id as email_id,
    m.from_text,
    c.id as crm_contact_id,
    c.display_name,
    ci.data as crm_email
FROM mail_mail m
CROSS JOIN crm_contact c
INNER JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE m.date_received >= DATE_SUB(NOW(), INTERVAL 2 HOUR)
AND ci.type = 1
AND c.status_id != 1
AND (
    LOWER(m.from_text) LIKE CONCAT('%', LOWER(ci.data), '%')
    OR LOWER(m.to_text) LIKE CONCAT('%', LOWER(ci.data), '%')
    OR LOWER(m.cc) LIKE CONCAT('%', LOWER(ci.data), '%')
)
ORDER BY m.date_received DESC;

-- 5. Check for unlinked emails that should be linked
SELECT 'Unlinked Emails with CRM Match Potential' as check_name;
SELECT 
    m.id,
    m.subject,
    m.from_text,
    m.to_text,
    m.date_received,
    'No CRM link found' as status
FROM mail_mail m
LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
WHERE m.date_received >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
AND l.id_chain IS NULL
AND m.folder IN (1, 2) -- Inbox and Sent
ORDER BY m.date_received DESC
LIMIT 10;

-- 6. Application logs check (if accessible)
SELECT 'Service Status Check' as check_name;
SELECT 
    'Check application logs for CRM Email Auto-Link Service messages' as instruction,
    'Look for: "CrmEmailAutoLinkService: Starting" or "Auto-linked email" messages' as what_to_find;