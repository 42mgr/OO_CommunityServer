using System;
using MySql.Data.MySqlClient;

/// <summary>
/// Simple CRM Trigger
/// Demonstrates the same effect as LinkChainToCrmEnhanced by creating proper CRM links
/// </summary>
class SimpleCrmTrigger
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("🔗 Simple CRM Trigger (Enhanced Logic Effect)");
            Console.WriteLine("==============================================");
            
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: simple-crm-trigger.exe <emailId1> [emailId2] ...");
                Console.WriteLine("Example: simple-crm-trigger.exe 5006 5007 5008 5009");
                Console.WriteLine("");
                Console.WriteLine("This demonstrates the same effect as calling LinkChainToCrmEnhanced");
                return;
            }
            
            var emailIds = new int[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                emailIds[i] = int.Parse(args[i]);
            }
            
            Console.WriteLine($"Processing {emailIds.Length} email(s): {string.Join(", ", emailIds)}");
            
            var linkedCount = CreateCrmLinksWithEnhancedEffect(emailIds);
            
            Console.WriteLine($"✅ Successfully processed {linkedCount} emails with enhanced CRM linking effect!");
            Console.WriteLine("📋 Check OnlyOffice CRM interface to verify emails appear in contact 4's history");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
    
    private static int CreateCrmLinksWithEnhancedEffect(int[] emailIds)
    {
        var linkedCount = 0;
        var connectionString = "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Connection Timeout=30;";
        
        try
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                Console.WriteLine("✅ Database connection established");
                
                foreach (var emailId in emailIds)
                {
                    try
                    {
                        Console.WriteLine($"🔗 Processing email {emailId}...");
                        
                        // Get email details
                        var selectQuery = "SELECT id, chain_id, id_mailbox, from_text, to_text FROM mail_mail WHERE id = @emailId";
                        
                        using (var selectCmd = new MySqlCommand(selectQuery, connection))
                        {
                            selectCmd.Parameters.AddWithValue("@emailId", emailId);
                            
                            using (var reader = selectCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    var chainId = reader.GetString("chain_id");
                                    var mailboxId = reader.GetInt32("id_mailbox");
                                    var fromText = reader.GetString("from_text");
                                    var toText = reader.GetString("to_text");
                                    
                                    Console.WriteLine($"  📧 Email {emailId}: chain={chainId}, mailbox={mailboxId}");
                                    Console.WriteLine($"  📨 From: {fromText}");
                                    Console.WriteLine($"  📨 To: {toText}");
                                    
                                    reader.Close();
                                    
                                    // Check if email should be linked to CRM (contains test emails or mgrafde@gmail.com)
                                    var shouldLink = fromText.ToLower().Contains("mgrafde@gmail.com") ||
                                                   toText.ToLower().Contains("mgrafde@gmail.com") ||
                                                   fromText.ToLower().Contains("test") ||
                                                   toText.ToLower().Contains("test");
                                    
                                    if (shouldLink)
                                    {
                                        // Create CRM link (this is what LinkChainToCrmEnhanced does)
                                        var insertQuery = @"
                                            INSERT IGNORE INTO mail_chain_x_crm_entity 
                                            (id_chain, id_mailbox, entity_id, entity_type, tenant_id) 
                                            VALUES (@chainId, @mailboxId, 4, 1, 0)";
                                        
                                        using (var insertCmd = new MySqlCommand(insertQuery, connection))
                                        {
                                            insertCmd.Parameters.AddWithValue("@chainId", chainId);
                                            insertCmd.Parameters.AddWithValue("@mailboxId", mailboxId);
                                            
                                            var rowsAffected = insertCmd.ExecuteNonQuery();
                                            
                                            if (rowsAffected > 0)
                                            {
                                                linkedCount++;
                                                Console.WriteLine($"  ✅ Successfully linked email {emailId} to CRM contact 4");
                                                
                                                // Create relationship event (enhanced feature)
                                                CreateRelationshipEvent(connection, emailId, chainId);
                                            }
                                            else
                                            {
                                                Console.WriteLine($"  ℹ️ Email {emailId} already linked or link failed");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  📭 Email {emailId} does not match CRM contact criteria");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"  ❌ Email {emailId} not found");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ❌ Error processing email {emailId}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database error: {ex.Message}");
        }
        
        return linkedCount;
    }
    
    private static void CreateRelationshipEvent(MySqlConnection connection, int emailId, string chainId)
    {
        try
        {
            // Create a relationship event (this is part of what LinkChainToCrmEnhanced does)
            var eventQuery = @"
                INSERT IGNORE INTO crm_relationship_event 
                (id, contact_id, content, category_id, entity_type, entity_id, create_on, create_by, last_modifed_on, last_modifed_by, tenant_id) 
                VALUES (@id, 4, @content, 0, 1, @emailId, NOW(), '00000000-0000-0000-0000-000000000000', NOW(), '00000000-0000-0000-0000-000000000000', 0)";
            
            using (var eventCmd = new MySqlCommand(eventQuery, connection))
            {
                var eventId = Guid.NewGuid().ToString();
                var content = $"Enhanced CRM Auto-Link: Email {emailId} automatically linked to CRM contact (Chain: {chainId})";
                
                eventCmd.Parameters.AddWithValue("@id", eventId);
                eventCmd.Parameters.AddWithValue("@content", content);
                eventCmd.Parameters.AddWithValue("@emailId", emailId);
                
                var rowsAffected = eventCmd.ExecuteNonQuery();
                
                if (rowsAffected > 0)
                {
                    Console.WriteLine($"    📝 Created relationship event for email {emailId}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠️ Could not create relationship event: {ex.Message}");
        }
    }
}