-- Fix the trigger JSON escaping issue and create backfill function

-- Step 1: Drop and recreate trigger with proper JSON escaping
DROP TRIGGER IF EXISTS auto_crm_link_emails;

DELIMITER $$

CREATE TRIGGER auto_crm_link_emails
AFTER INSERT ON mail_mail
FOR EACH ROW
BEGIN
    DECLARE contact_count INT DEFAULT 0;
    DECLARE contact_id_found INT DEFAULT 0;
    DECLARE email_address VARCHAR(255) DEFAULT '';
    DECLARE json_content TEXT DEFAULT '';
    DECLARE escaped_from TEXT DEFAULT '';
    DECLARE escaped_to TEXT DEFAULT '';
    DECLARE escaped_cc TEXT DEFAULT '';
    DECLARE escaped_subject TEXT DEFAULT '';
    DECLARE escaped_intro TEXT DEFAULT '';
    
    -- Only process inbox emails (folder = 2)
    IF NEW.folder = 2 THEN
        -- Extract email address from from_text field
        SET email_address = TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(NEW.from_text, '<', -1), '>', 1));
        
        -- If no < > brackets, use the whole from_text as email
        IF email_address = NEW.from_text THEN
            SET email_address = TRIM(NEW.from_text);
        END IF;
        
        -- Find matching CRM contact by email address
        SELECT COUNT(*), MIN(c.id) INTO contact_count, contact_id_found
        FROM crm_contact c
        JOIN crm_contact_info ci ON c.id = ci.contact_id
        WHERE ci.data = email_address
        AND c.tenant_id = NEW.tenant
        AND ci.type = 1;  -- Email type
        
        -- If contact found, create CRM relationship event
        IF contact_count > 0 THEN
            -- Properly escape JSON strings
            SET escaped_from = REPLACE(REPLACE(NEW.from_text, '\\', '\\\\'), '"', '\\"');
            SET escaped_to = REPLACE(REPLACE(IFNULL(NEW.to_text, ''), '\\', '\\\\'), '"', '\\"');
            SET escaped_cc = REPLACE(REPLACE(IFNULL(NEW.cc, ''), '\\', '\\\\'), '"', '\\"');
            SET escaped_subject = REPLACE(REPLACE(IFNULL(NEW.subject, ''), '\\', '\\\\'), '"', '\\"');
            SET escaped_intro = REPLACE(REPLACE(IFNULL(NEW.introduction, ''), '\\', '\\\\'), '"', '\\"');
            
            -- Build properly escaped JSON content
            SET json_content = CONCAT(
                '{"from":"', escaped_from,
                '","to":"', escaped_to,
                '","cc":"', escaped_cc,
                '","bcc":"","subject":"', escaped_subject,
                '","important":', IF(NEW.importance, 'true', 'false'),
                ',"chain_id":"', IFNULL(NEW.chain_id, ''),
                '","is_sended":false,"date_created":"', DATE_FORMAT(NEW.date_received, '%m/%d/%Y %H:%i:%s'),
                '","introduction":"', escaped_intro,
                '","message_id":', NEW.id,
                ',"message_url":"/Products/CRM/HttpHandlers/filehandler.ashx?action=mailmessage&message_id=', NEW.id, '"}'
            );
            
            -- Insert CRM relationship event
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
            ) VALUES (
                NEW.tenant,
                contact_id_found,
                json_content,
                NEW.date_received,
                NEW.id_user,
                0,  -- Entity type for email
                NEW.id,
                -3, -- Email category
                NEW.date_received,
                NEW.id_user,
                0   -- No files initially
            );
        END IF;
    END IF;
END$$

DELIMITER ;

-- Step 2: Function to backfill existing unlinked emails
-- This will process all unlinked inbox emails and create CRM events for them

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
  AND m.date_received > '2025-06-09 09:00:00'  -- Recent emails
  AND ci.type = 1  -- Email type
  AND ci.data = TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1))
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  );

-- Step 3: Show results
SELECT 
    'BACKFILL RESULTS' as analysis_type,
    COUNT(*) as emails_linked
FROM crm_relationship_event 
WHERE create_on > NOW() - INTERVAL 1 MINUTE
  AND entity_type = 0;

-- Step 4: Validate all CRM events now have valid JSON
SELECT 
    'JSON VALIDATION AFTER FIX' as analysis_type,
    entity_id as email_id,
    contact_id,
    CASE 
        WHEN JSON_VALID(content) THEN 'VALID JSON'
        ELSE 'INVALID JSON'
    END as json_status,
    LEFT(content, 80) as content_preview
FROM crm_relationship_event 
WHERE entity_type = 0
  AND entity_id >= 330  -- Our test emails
ORDER BY entity_id;