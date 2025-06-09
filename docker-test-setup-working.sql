-- Working test data setup for CRM Email Monitoring
-- This version uses only columns that actually exist in the database

USE onlyoffice;

-- Check if we have any existing data first
SELECT 'Checking existing data...' as step;

-- Create test CRM contacts (simplified - using only essential columns)
INSERT IGNORE INTO crm_contact (id, tenant_id, is_company, first_name, last_name, company_name, display_name, create_by, create_on)
VALUES 
(1001, 1, 0, 'John', 'Doe', NULL, 'John Doe', '00000000-0000-0000-0000-000000000000', NOW()),
(1002, 1, 0, 'Jane', 'Smith', NULL, 'Jane Smith', '00000000-0000-0000-0000-000000000000', NOW()),
(1003, 1, 1, NULL, NULL, 'Acme Corp', 'Acme Corp', '00000000-0000-0000-0000-000000000000', NOW());

-- Add email addresses for the test contacts
INSERT IGNORE INTO crm_contact_info (contact_id, data, type, category, tenant_id, is_primary)
VALUES 
(1001, 'john.doe@example.com', 1, 0, 1, 1),
(1001, 'j.doe@company.com', 1, 1, 1, 0),
(1002, 'jane.smith@example.com', 1, 0, 1, 1),
(1002, 'jsmith@business.org', 1, 1, 1, 0),
(1003, 'info@acmecorp.com', 1, 0, 1, 1),
(1003, 'sales@acmecorp.com', 1, 1, 1, 0);

-- Create test emails using only existing columns
INSERT IGNORE INTO mail_mail (
    id, id_mailbox, id_user, tenant, address, from_text, to_text, cc, subject, 
    introduction, importance, date_received, date_sent, size, attachments_count, 
    unread, is_answered, is_forwarded, is_from_crm, is_from_tl, is_text_body_only, 
    has_parse_error, folder, folder_restore, spam, chain_id, chain_date, stream
)
VALUES 
-- Incoming email from John Doe
(2001, 1, 'admin', 1, 'admin@onlyoffice.com', 'John Doe <john.doe@example.com>', 
 'admin@onlyoffice.com', '', 'Test Email from John Doe', 
 'This is a test email from John Doe for CRM testing', 0, NOW(), DATE_SUB(NOW(), INTERVAL 1 MINUTE), 
 1024, 0, 1, 0, 0, 0, 0, 1, 0, 2, 1, 0, 'chain_test_001', NOW(), ''),

-- Incoming email from Jane Smith  
(2002, 1, 'admin', 1, 'admin@onlyoffice.com', 'Jane Smith <jane.smith@example.com>', 
 'admin@onlyoffice.com', '', 'Re: Project Discussion', 
 'Follow up on our project discussion', 0, DATE_SUB(NOW(), INTERVAL 30 SECOND), DATE_SUB(NOW(), INTERVAL 35 SECOND), 
 2048, 0, 1, 0, 0, 0, 0, 1, 0, 2, 1, 0, 'chain_test_002', DATE_SUB(NOW(), INTERVAL 30 SECOND), ''),

-- Outgoing email to Acme Corp
(2003, 1, 'admin', 1, 'admin@onlyoffice.com', 'admin@onlyoffice.com', 
 'info@acmecorp.com', 'sales@acmecorp.com', 'Business Proposal', 
 'Please find our business proposal attached', 0, DATE_SUB(NOW(), INTERVAL 10 SECOND), DATE_SUB(NOW(), INTERVAL 15 SECOND), 
 3072, 1, 0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 'chain_test_003', DATE_SUB(NOW(), INTERVAL 10 SECOND), ''),

-- Recent email that should trigger the monitoring
(2004, 1, 'admin', 1, 'admin@onlyoffice.com', 'J. Doe <j.doe@company.com>', 
 'admin@onlyoffice.com', '', 'Urgent: Follow up needed', 
 'This is an urgent follow up email from John secondary email', 1, NOW(), DATE_SUB(NOW(), INTERVAL 5 SECOND), 
 1536, 0, 1, 0, 0, 0, 0, 1, 0, 2, 1, 0, 'chain_test_004', NOW(), '');

-- Verify the test data
SELECT 'Test data created successfully!' as message;

-- Show what we created
SELECT 'CRM Contacts with emails:' as info;
SELECT c.id, c.display_name, ci.data as email_address
FROM crm_contact c
LEFT JOIN crm_contact_info ci ON c.id = ci.contact_id AND ci.type = 1
WHERE c.id >= 1001
ORDER BY c.id, ci.is_primary DESC;

SELECT 'Test Emails created:' as info;
SELECT id, from_text, to_text, subject, date_received, folder,
       CASE folder 
           WHEN 1 THEN 'Sent'
           WHEN 2 THEN 'Inbox'
           ELSE 'Other'
       END as folder_name
FROM mail_mail 
WHERE id >= 2001
ORDER BY date_received DESC;

SELECT 'Emails ready for CRM linking:' as info;
SELECT COUNT(*) as unprocessed_emails
FROM mail_mail m
WHERE m.id >= 2001
  AND m.folder IN (1, 2)
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  );

-- Show potential matches that should be found by the monitoring
SELECT 'Expected CRM matches:' as info;
SELECT 
    m.id as email_id,
    m.subject,
    SUBSTRING(m.from_text, 1, 50) as from_email,
    c.id as contact_id,
    c.display_name as contact_name,
    ci.data as matching_email
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
ORDER BY m.id, c.id;