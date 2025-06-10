using System;
using MySql.Data.MySqlClient;

/// <summary>
/// Simple CRM Email Linker
/// Creates database links for Test 30 and Test 31 emails to CRM contact 4
/// </summary>
class LinkTestEmails
{
    static void Main(string[] args)
    {
        var connectionString = "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Connection Timeout=30;";
        
        try
        {
            Console.WriteLine("🔗 Linking Test 30 and Test 31 emails to CRM contact 4...");
            
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                Console.WriteLine("✅ Connected to database");
                
                // Get email details and create links
                var emailIds = new int[] { 5006, 5007, 5008, 5009 };
                var emailNames = new string[] { "Test 30 (inbox)", "Test 30 (sent)", "Test 31 (inbox)", "Test 31 (sent)" };
                
                for (int i = 0; i < emailIds.Length; i++)
                {
                    LinkEmailToCrm(connection, emailIds[i], emailNames[i]);
                }
                
                Console.WriteLine("🎉 CRM linking completed!");
                Console.WriteLine("📋 Check the OnlyOffice CRM interface to verify emails appear in contact 4's history");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
    
    static void LinkEmailToCrm(MySqlConnection connection, int emailId, string emailName)
    {
        try
        {
            Console.WriteLine($"🔧 Processing {emailName} (ID: {emailId})...");
            
            // Get email chain and mailbox info
            var selectQuery = "SELECT chain_id, id_mailbox FROM mail_mail WHERE id = @emailId";
            using (var selectCmd = new MySqlCommand(selectQuery, connection))
            {
                selectCmd.Parameters.AddWithValue("@emailId", emailId);
                using (var reader = selectCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var chainId = reader.GetString("chain_id");
                        var mailboxId = reader.GetInt32("id_mailbox");
                        reader.Close();
                        
                        // Create CRM link
                        var insertQuery = @"
                            INSERT IGNORE INTO mail_chain_x_crm_entity 
                            (id_chain, id_mailbox, entity_id, entity_type) 
                            VALUES (@chainId, @mailboxId, 4, 1)";
                        
                        using (var insertCmd = new MySqlCommand(insertQuery, connection))
                        {
                            insertCmd.Parameters.AddWithValue("@chainId", chainId);
                            insertCmd.Parameters.AddWithValue("@mailboxId", mailboxId);
                            
                            var rowsAffected = insertCmd.ExecuteNonQuery();
                            
                            if (rowsAffected > 0)
                            {
                                Console.WriteLine($"  ✅ Successfully linked {emailName} to CRM contact 4");
                            }
                            else
                            {
                                Console.WriteLine($"  ℹ️ {emailName} already linked or link failed");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ❌ {emailName} not found in database");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error linking {emailName}: {ex.Message}");
        }
    }
}