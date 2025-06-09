-- Create MySQL trigger for automatic CRM linking of incoming emails
-- This trigger fires when new emails are inserted into mail_mail table

DELIMITER $$

DROP TRIGGER IF EXISTS auto_crm_link_emails$$

CREATE TRIGGER auto_crm_link_emails
AFTER INSERT ON mail_mail
FOR EACH ROW
BEGIN
    DECLARE contact_count INT DEFAULT 0;
    DECLARE contact_id_found INT DEFAULT 0;
    DECLARE email_address VARCHAR(255) DEFAULT '';
    DECLARE json_content TEXT DEFAULT '';
    
    -- Only process inbox emails (folder = 2)
    IF NEW.folder = 2 THEN
        -- Extract email address from from_text field
        -- Format: "Name" <email@domain.com> or just email@domain.com
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
            -- Build JSON content for the relationship event
            SET json_content = CONCAT(
                '{"from":"', REPLACE(NEW.from_text, '"', '\\"'), 
                '","to":"', IFNULL(REPLACE(NEW.to_text, '"', '\\"'), ''),
                '","cc":"', IFNULL(REPLACE(NEW.cc, '"', '\\"'), ''),
                '","bcc":"", "subject":"', REPLACE(NEW.subject, '"', '\\"'),
                '","important":', IF(NEW.importance, 'true', 'false'),
                ',"chain_id":"', IFNULL(NEW.chain_id, ''),
                '","is_sended":false,"date_created":"', DATE_FORMAT(NEW.date_received, '%m/%d/%Y %H:%i:%s'),
                '","introduction":"', IFNULL(REPLACE(NEW.introduction, '"', '\\"'), ''),
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

-- Test the trigger by showing what it would have processed
SELECT 
    m.id as email_id,
    m.subject,
    m.from_text,
    TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)) as extracted_email,
    c.id as contact_id,
    CONCAT(c.first_name, ' ', c.last_name) as contact_name,
    'Would be auto-linked' as status
FROM mail_mail m
JOIN crm_contact c ON c.tenant_id = m.tenant
JOIN crm_contact_info ci ON c.id = ci.contact_id
WHERE m.tenant = 1 
  AND m.folder = 2
  AND m.date_received > '2025-06-09 09:00:00'
  AND ci.type = 1
  AND ci.data = TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1))
  AND NOT EXISTS (
      SELECT 1 FROM crm_relationship_event cre 
      WHERE cre.entity_type = 0 AND cre.entity_id = m.id
  )
ORDER BY m.date_received DESC;