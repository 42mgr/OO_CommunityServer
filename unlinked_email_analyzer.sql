-- Function to search database for unlinked emails and match with CRM contact emails
-- This provides a comprehensive analysis before implementing automatic triggers

-- Create a function/procedure to analyze unlinked emails
DELIMITER $$

DROP PROCEDURE IF EXISTS AnalyzeUnlinkedEmails$$

CREATE PROCEDURE AnalyzeUnlinkedEmails(
    IN days_back INT,
    IN tenant_id_param INT
)
BEGIN
    DECLARE done INT DEFAULT FALSE;
    DECLARE v_email_id INT;
    DECLARE v_from_email VARCHAR(255);
    DECLARE v_subject VARCHAR(500);
    DECLARE v_date_received DATETIME;
    DECLARE v_folder INT;
    DECLARE match_count INT;
    DECLARE contact_id_found INT;
    DECLARE contact_name_found VARCHAR(500);
    
    -- Cursor for unlinked emails
    DECLARE email_cursor CURSOR FOR
        SELECT 
            m.id,
            TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(m.from_text, '<', -1), '>', 1)) as extracted_email,
            m.subject,
            m.date_received,
            m.folder
        FROM mail_mail m
        WHERE m.tenant = tenant_id_param
          AND m.date_received > DATE_SUB(NOW(), INTERVAL days_back DAY)
          AND NOT EXISTS (
              SELECT 1 FROM crm_relationship_event cre 
              WHERE cre.entity_type = 0 AND cre.entity_id = m.id
          )
        ORDER BY m.date_received DESC;
    
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
    
    -- Create temporary results table
    CREATE TEMPORARY TABLE IF NOT EXISTS temp_unlinked_analysis (
        email_id INT,
        from_email VARCHAR(255),
        subject VARCHAR(500),
        date_received DATETIME,
        folder_type VARCHAR(20),
        match_status VARCHAR(50),
        contact_id INT,
        contact_name VARCHAR(500),
        recommendation VARCHAR(200)
    );
    
    -- Clear previous results
    DELETE FROM temp_unlinked_analysis;
    
    -- Process each unlinked email
    OPEN email_cursor;
    
    read_loop: LOOP
        FETCH email_cursor INTO v_email_id, v_from_email, v_subject, v_date_received, v_folder;
        
        IF done THEN
            LEAVE read_loop;
        END IF;
        
        -- Check for matching CRM contact
        SELECT COUNT(*), MIN(c.id), MIN(CONCAT(c.first_name, ' ', c.last_name))
        INTO match_count, contact_id_found, contact_name_found
        FROM crm_contact c
        JOIN crm_contact_info ci ON c.id = ci.contact_id
        WHERE ci.data = v_from_email
          AND c.tenant_id = tenant_id_param
          AND ci.type = 1;  -- Email type
        
        -- Insert analysis result
        INSERT INTO temp_unlinked_analysis VALUES (
            v_email_id,
            v_from_email,
            v_subject,
            v_date_received,
            CASE 
                WHEN v_folder = 1 THEN 'Sent'
                WHEN v_folder = 2 THEN 'Inbox'
                WHEN v_folder = 3 THEN 'Spam'
                ELSE CONCAT('Folder_', v_folder)
            END,
            CASE 
                WHEN match_count > 0 THEN 'CRM_CONTACT_FOUND'
                ELSE 'NO_CRM_MATCH'
            END,
            contact_id_found,
            contact_name_found,
            CASE 
                WHEN match_count > 0 AND v_folder = 2 THEN 'SHOULD_AUTO_LINK'
                WHEN match_count > 0 AND v_folder = 1 THEN 'OUTGOING_EMAIL'
                WHEN match_count = 0 THEN 'CREATE_NEW_CONTACT'
                ELSE 'NO_ACTION_NEEDED'
            END
        );
        
    END LOOP;
    
    CLOSE email_cursor;
    
    -- Return comprehensive analysis results
    SELECT 
        'UNLINKED EMAIL ANALYSIS' as report_section,
        email_id,
        from_email,
        subject,
        date_received,
        folder_type,
        match_status,
        contact_id,
        contact_name,
        recommendation
    FROM temp_unlinked_analysis
    ORDER BY date_received DESC;
    
    -- Summary statistics
    SELECT 
        'SUMMARY STATISTICS' as report_section,
        COUNT(*) as total_unlinked_emails,
        SUM(CASE WHEN match_status = 'CRM_CONTACT_FOUND' THEN 1 ELSE 0 END) as emails_with_crm_matches,
        SUM(CASE WHEN recommendation = 'SHOULD_AUTO_LINK' THEN 1 ELSE 0 END) as should_auto_link,
        SUM(CASE WHEN recommendation = 'CREATE_NEW_CONTACT' THEN 1 ELSE 0 END) as create_new_contacts,
        SUM(CASE WHEN folder_type = 'Inbox' THEN 1 ELSE 0 END) as inbox_emails,
        SUM(CASE WHEN folder_type = 'Sent' THEN 1 ELSE 0 END) as sent_emails
    FROM temp_unlinked_analysis;
    
    -- List of unique email addresses that could become new CRM contacts
    SELECT 
        'POTENTIAL_NEW_CONTACTS' as report_section,
        from_email,
        COUNT(*) as email_count,
        MIN(date_received) as first_email,
        MAX(date_received) as last_email,
        'Consider creating CRM contact' as recommendation
    FROM temp_unlinked_analysis
    WHERE match_status = 'NO_CRM_MATCH'
      AND folder_type = 'Inbox'
    GROUP BY from_email
    HAVING COUNT(*) >= 1  -- Show all for now, could filter for COUNT(*) >= 2 for frequent senders
    ORDER BY email_count DESC, last_email DESC;
    
    -- Clean up
    DROP TEMPORARY TABLE temp_unlinked_analysis;
    
END$$

DELIMITER ;

-- Call the analysis function for the last 7 days
CALL AnalyzeUnlinkedEmails(7, 1);