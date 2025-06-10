<%@ WebHandler Language="C#" Class="ManualCrmLink" %>

using System;
using System.Web;
using System.Linq;

public class ManualCrmLink : IHttpHandler 
{
    public void ProcessRequest(HttpContext context) 
    {
        try
        {
            context.Response.ContentType = "text/plain";
            
            // Simple manual linking logic using direct SQL
            string result = "🔗 Manual CRM Linking Results:\n\n";
            
            // Get recent unlinked emails
            var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["default"].ConnectionString;
            
            using (var connection = new MySql.Data.MySqlClient.MySqlConnection(connectionString))
            {
                connection.Open();
                
                // Find recent unlinked emails from mgrafde@gmail.com
                var query = @"
                    SELECT m.id, m.chain_id, m.id_mailbox, m.from_text, m.subject, m.date_received
                    FROM mail_mail m
                    LEFT JOIN mail_chain_x_crm_entity l ON m.chain_id = l.id_chain AND m.id_mailbox = l.id_mailbox
                    WHERE m.tenant = 1 
                    AND m.date_received >= DATE_SUB(NOW(), INTERVAL 2 HOUR)
                    AND m.folder IN (1, 2)
                    AND l.id_chain IS NULL
                    AND (m.from_text LIKE '%mgrafde@gmail.com%' OR m.to_text LIKE '%mgrafde@gmail.com%')
                    ORDER BY m.date_received DESC
                    LIMIT 10";
                
                using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(query, connection))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        var emailsToLink = new System.Collections.Generic.List<dynamic>();
                        
                        while (reader.Read())
                        {
                            result += $"Found email: ID={reader["id"]}, Subject='{reader["subject"]}', From='{reader["from_text"]}'\n";
                            emailsToLink.Add(new { 
                                Id = reader.GetInt32("id"),
                                ChainId = reader.GetString("chain_id"),
                                MailboxId = reader.GetInt32("id_mailbox")
                            });
                        }
                        
                        reader.Close();
                        
                        // Now link them to CRM contact 4
                        foreach (var email in emailsToLink)
                        {
                            var linkQuery = @"
                                INSERT IGNORE INTO mail_chain_x_crm_entity 
                                (id_chain, id_mailbox, entity_id, entity_type, last_modified_on, last_modified_by) 
                                VALUES (@chain_id, @mailbox_id, 4, 1, NOW(), UUID())";
                            
                            using (var linkCmd = new MySql.Data.MySqlClient.MySqlCommand(linkQuery, connection))
                            {
                                linkCmd.Parameters.AddWithValue("@chain_id", email.ChainId);
                                linkCmd.Parameters.AddWithValue("@mailbox_id", email.MailboxId);
                                
                                var rows = linkCmd.ExecuteNonQuery();
                                result += $"✅ Linked email {email.Id} to CRM contact 4 (rows affected: {rows})\n";
                            }
                        }
                        
                        if (emailsToLink.Count == 0)
                        {
                            result += "ℹ️ No unlinked emails found in the last 2 hours\n";
                        }
                    }
                }
            }
            
            context.Response.Write(result);
        }
        catch (Exception ex)
        {
            context.Response.ContentType = "text/plain";
            context.Response.Write($"❌ Error: {ex.Message}\n\nStack trace: {ex.StackTrace}");
        }
    }
 
    public bool IsReusable 
    {
        get { return false; }
    }
}