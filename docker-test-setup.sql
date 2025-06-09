-- Test data setup for CRM Email Monitoring
-- Run this script to create sample data for testing the monitoring system

USE onlyoffice;

-- Create a test tenant (if needed)
INSERT IGNORE INTO tenants_tenants (id, name, alias, mappeddomain, version, version_changed, language, timezone, trusteddomains, trusteddomainsenabled, status, statuschanged, creationdatetime, owner_id, public, publicvisibleproducts, payment_id)
VALUES (1, 'Test Tenant', 'test', NULL, 0, NOW(), 'en-US', NULL, NULL, 0, 0, NOW(), NOW(), '00000000-0000-0000-0000-000000000000', 0, NULL, NULL);

-- Create a test user (if needed)
INSERT IGNORE INTO core_user (id, tenant, username, firstname, lastname, sex, bithdate, status, activation_status, email, workfromdate, terminateddate, last_modified, create_on, phone, phone_activation, location, notes, sid, sso_name_id, sso_session_id, removed, culture, contacts, title)
VALUES ('11111111-1111-1111-1111-111111111111', 1, 'testuser', 'Test', 'User', NULL, NULL, 1, 0, 'testuser@example.com', NOW(), NULL, NOW(), NOW(), NULL, 0, NULL, NULL, NULL, NULL, NULL, 0, NULL, NULL, 'Test User');

-- Create test CRM contacts
INSERT IGNORE INTO crm_contact (id, tenant_id, is_company, first_name, last_name, company_name, display_name, create_by, create_on, last_modifed_by, last_modifed_on, status)
VALUES 
(1, 1, 0, 'John', 'Doe', NULL, 'John Doe', '11111111-1111-1111-1111-111111111111', NOW(), '11111111-1111-1111-1111-111111111111', NOW(), 0),
(2, 1, 0, 'Jane', 'Smith', NULL, 'Jane Smith', '11111111-1111-1111-1111-111111111111', NOW(), '11111111-1111-1111-1111-111111111111', NOW(), 0),
(3, 1, 1, NULL, NULL, 'Acme Corp', 'Acme Corp', '11111111-1111-1111-1111-111111111111', NOW(), '11111111-1111-1111-1111-111111111111', NOW(), 0);

-- Add email addresses for the test contacts
INSERT IGNORE INTO crm_contact_info (id, contact_id, data, type, category, tenant_id, is_primary)
VALUES 
(1, 1, 'john.doe@example.com', 1, 0, 1, 1),
(2, 1, 'j.doe@company.com', 1, 1, 1, 0),
(3, 2, 'jane.smith@example.com', 1, 0, 1, 1),
(4, 2, 'jsmith@business.org', 1, 1, 1, 0),
(5, 3, 'info@acmecorp.com', 1, 0, 1, 1),
(6, 3, 'sales@acmecorp.com', 1, 1, 1, 0);

-- Create a test mailbox
INSERT IGNORE INTO mail_mailbox (id, tenant, id_user, address, name, enabled, is_processed, is_server_mailbox, imap_port, imap_server, smtp_port, smtp_server, account, password, size_limit, login_delay, quota_error, auth_error, imap_intervals, begin_date, email_in_folder, pop3_password, smtp_password, enabled_oauth, oauth_token, oauth_refresh_token, imap_encryption, smtp_encryption, auth_type_in, auth_type_smtp)
VALUES (1, 1, '11111111-1111-1111-1111-111111111111', 'testuser@example.com', 'Test Mailbox', 1, 1, 0, 993, 'imap.example.com', 587, 'smtp.example.com', 'testuser@example.com', 'encrypted_password', 0, 30, 0, 0, 30, NOW(), 'INBOX', NULL, NULL, 0, NULL, NULL, 1, 1, 1, 1);

-- Create test email chains
INSERT IGNORE INTO mail_chain (id, id_mailbox, tenant, id_user, folder, length, unread, has_attachments, importance, tags, is_crm_chain)
VALUES 
('chain_001', 1, 1, '11111111-1111-1111-1111-111111111111', 2, 1, 1, 0, 0, '', 0),
('chain_002', 1, 1, '11111111-1111-1111-1111-111111111111', 2, 1, 1, 0, 0, '', 0),
('chain_003', 1, 1, '11111111-1111-1111-1111-111111111111', 1, 1, 0, 0, 0, '', 0);

-- Create test emails that should be linked to CRM
INSERT IGNORE INTO mail_mail (id, id_mailbox, id_user, tenant, address, from_text, to_text, cc, bcc, subject, introduction, importance, date_received, date_sent, size, attachments_count, unread, is_answered, is_forwarded, is_from_crm, is_from_tl, reply_to, chain_id, chain_date, is_text_body_only, has_parse_error, calendar_uid, folder, restore_folder, spam, time_modified, introduction_format, message_id, in_reply_to, mime_reply_to, stream)
VALUES 
-- Incoming email from John Doe
(1, 1, '11111111-1111-1111-1111-111111111111', 1, 'testuser@example.com', 'John Doe <john.doe@example.com>', 'testuser@example.com', '', '', 'Test Email Subject 1', 'This is a test email from John Doe', 0, NOW(), DATE_SUB(NOW(), INTERVAL 1 MINUTE), 1024, 0, 1, 0, 0, 0, 0, '', 'chain_001', NOW(), 1, 0, '', 2, 1, 0, NOW(), 0, 'msg001@example.com', '', '', ''),

-- Incoming email from Jane Smith  
(2, 1, '11111111-1111-1111-1111-111111111111', 1, 'testuser@example.com', 'Jane Smith <jane.smith@example.com>', 'testuser@example.com', '', '', 'Re: Project Discussion', 'Follow up on our project discussion', 0, DATE_SUB(NOW(), INTERVAL 30 SECOND), DATE_SUB(NOW(), INTERVAL 35 SECOND), 2048, 0, 1, 0, 0, 0, 0, '', 'chain_002', DATE_SUB(NOW(), INTERVAL 30 SECOND), 1, 0, '', 2, 1, 0, NOW(), 0, 'msg002@example.com', '', '', ''),

-- Outgoing email to Acme Corp
(3, 1, '11111111-1111-1111-1111-111111111111', 1, 'testuser@example.com', 'testuser@example.com', 'info@acmecorp.com', 'sales@acmecorp.com', '', 'Business Proposal', 'Please find our business proposal attached', 0, DATE_SUB(NOW(), INTERVAL 10 SECOND), DATE_SUB(NOW(), INTERVAL 15 SECOND), 3072, 1, 0, 0, 0, 0, 0, '', 'chain_003', DATE_SUB(NOW(), INTERVAL 10 SECOND), 1, 0, '', 1, 1, 0, NOW(), 0, 'msg003@example.com', '', '', ''),

-- Recent email that should trigger the monitoring (within last few minutes)
(4, 1, '11111111-1111-1111-1111-111111111111', 1, 'testuser@example.com', 'J. Doe <j.doe@company.com>', 'testuser@example.com', '', '', 'Urgent: Follow up needed', 'This is an urgent follow up email', 1, NOW(), DATE_SUB(NOW(), INTERVAL 5 SECOND), 1536, 0, 1, 0, 0, 0, 0, '', 'chain_004', NOW(), 1, 0, '', 2, 1, 0, NOW(), 0, 'msg004@company.com', '', '', '');

-- Add the new chain for the recent email
INSERT IGNORE INTO mail_chain (id, id_mailbox, tenant, id_user, folder, length, unread, has_attachments, importance, tags, is_crm_chain)
VALUES ('chain_004', 1, 1, '11111111-1111-1111-1111-111111111111', 2, 1, 1, 0, 0, '', 0);

-- Verify the test data
SELECT 'Test data created successfully!' as message;

-- Show what we created
SELECT 'CRM Contacts:' as info;
SELECT c.id, c.display_name, ci.data as email_address
FROM crm_contact c
LEFT JOIN crm_contact_info ci ON c.id = ci.contact_id AND ci.type = 1
WHERE c.tenant_id = 1;

SELECT 'Test Emails:' as info;
SELECT id, from_text, to_text, subject, date_received, folder
FROM mail_mail 
WHERE tenant = 1
ORDER BY date_received DESC;

SELECT 'Emails ready for CRM linking:' as info;
SELECT COUNT(*) as unprocessed_emails
FROM mail_mail m
WHERE m.tenant = 1
  AND m.folder IN (1, 2)
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  );
