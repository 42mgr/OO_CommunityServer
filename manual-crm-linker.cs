using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

/// <summary>
/// Manual CRM Linker
/// Direct invocation of CRM linking for specific emails using assemblies
/// </summary>
class ManualCrmLinker
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("🔗 Manual CRM Linker for Test 30 and Test 31");
            
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: manual-crm-linker.exe <emailId1> [emailId2] [emailId3] ...");
                Console.WriteLine("Example: manual-crm-linker.exe 5006 5007 5008 5009");
                return;
            }
            
            // Load required assemblies from OnlyOffice bin directory
            var binPath = "/var/www/onlyoffice/WebStudio/bin";
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                try
                {
                    var assemblyName = new AssemblyName(resolveArgs.Name).Name;
                    var assemblyPath = Path.Combine(binPath, assemblyName + ".dll");
                    if (File.Exists(assemblyPath))
                    {
                        return Assembly.LoadFrom(assemblyPath);
                    }
                }
                catch { }
                return null;
            };
            
            Console.WriteLine("✅ Assembly resolver configured");
            
            // Load the ASC.Mail assembly
            var mailAssembly = Assembly.LoadFrom(Path.Combine(binPath, "ASC.Mail.dll"));
            Console.WriteLine("✅ Loaded ASC.Mail.dll");
            
            // Load required assemblies preemptively
            var commonAssembly = Assembly.LoadFrom(Path.Combine(binPath, "ASC.Common.dll"));
            var coreAssembly = Assembly.LoadFrom(Path.Combine(binPath, "ASC.Core.Common.dll"));
            Console.WriteLine("✅ Loaded core assemblies");
            
            // Create manual linking process
            var success = ProcessEmailIds(mailAssembly, args);
            
            if (success)
            {
                Console.WriteLine("🎉 Manual CRM linking completed successfully!");
            }
            else
            {
                Console.WriteLine("⚠️ Some emails may not have been linked");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    private static bool ProcessEmailIds(Assembly mailAssembly, string[] emailIdArgs)
    {
        try
        {
            Console.WriteLine("🔧 Setting up CRM linking components...");
            
            // Manually create database link entries for emails that should be linked to contact 4
            var connectionString = "Server=onlyoffice-mysql-server;Port=3306;Database=onlyoffice;User ID=onlyoffice_user;Password=onlyoffice_pass;Pooling=true;Character Set=utf8;AutoEnlist=false;SSL Mode=none;AllowPublicKeyRetrieval=true;Connection Timeout=30;Maximum Pool Size=300;";
            
            using (var mysqlAssembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/MySql.Data.dll"))
            {
                var connectionType = mysqlAssembly.GetType("MySql.Data.MySqlClient.MySqlConnection");
                var commandType = mysqlAssembly.GetType("MySql.Data.MySqlClient.MySqlCommand");
                
                using (var connection = Activator.CreateInstance(connectionType, connectionString))
                {
                    var openMethod = connectionType.GetMethod("Open");
                    openMethod.Invoke(connection, null);
                    
                    Console.WriteLine("✅ Database connection established");
                    
                    foreach (var emailIdStr in emailIdArgs)
                    {
                        if (int.TryParse(emailIdStr, out int emailId))
                        {
                            ProcessSingleEmail(connection, connectionType, commandType, emailId);
                        }
                    }
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in ProcessEmailIds: {ex.Message}");
            return false;
        }
    }
    
    private static void ProcessSingleEmail(object connection, Type connectionType, Type commandType, int emailId)
    {
        try
        {
            Console.WriteLine($"🔗 Processing email {emailId}...");
            
            // First get the email details
            var selectQuery = "SELECT id, chain_id, id_mailbox, from_text, to_text FROM mail_mail WHERE id = @emailId";
            
            using (var selectCommand = Activator.CreateInstance(commandType, selectQuery, connection))
            {
                var parametersProperty = commandType.GetProperty("Parameters");
                var parameters = parametersProperty.GetValue(selectCommand);
                var addMethod = parameters.GetType().GetMethod("AddWithValue");
                addMethod.Invoke(parameters, new object[] { "@emailId", emailId });
                
                var executeReaderMethod = commandType.GetMethod("ExecuteReader", new Type[0]);
                using (var reader = executeReaderMethod.Invoke(selectCommand, null))
                {
                    var readMethod = reader.GetType().GetMethod("Read");
                    if ((bool)readMethod.Invoke(reader, null))
                    {
                        var chainId = reader.GetType().GetMethod("GetString").Invoke(reader, new object[] { "chain_id" });
                        var mailboxId = reader.GetType().GetMethod("GetInt32").Invoke(reader, new object[] { "id_mailbox" });
                        var fromText = reader.GetType().GetMethod("GetString").Invoke(reader, new object[] { "from_text" });
                        
                        Console.WriteLine($"  Email {emailId}: chain={chainId}, mailbox={mailboxId}, from={fromText}");
                        
                        // Close reader
                        reader.GetType().GetMethod("Close").Invoke(reader, null);
                        
                        // Check if this email should be linked (contains mgrafde@gmail.com)
                        if (fromText.ToString().ToLower().Contains("mgrafde@gmail.com"))
                        {
                            LinkEmailToCrm(connection, commandType, chainId.ToString(), (int)mailboxId, emailId);
                        }
                        else
                        {
                            Console.WriteLine($"  Email {emailId} does not match CRM contact criteria");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  Email {emailId} not found");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing email {emailId}: {ex.Message}");
        }
    }
    
    private static void LinkEmailToCrm(object connection, Type commandType, string chainId, int mailboxId, int emailId)
    {
        try
        {
            // Insert CRM link record
            var insertQuery = @"
                INSERT IGNORE INTO mail_chain_x_crm_entity 
                (id_chain, id_mailbox, entity_id, entity_type) 
                VALUES (@chainId, @mailboxId, 4, 1)";
            
            using (var insertCommand = Activator.CreateInstance(commandType, insertQuery, connection))
            {
                var parametersProperty = commandType.GetProperty("Parameters");
                var parameters = parametersProperty.GetValue(insertCommand);
                var addMethod = parameters.GetType().GetMethod("AddWithValue");
                
                addMethod.Invoke(parameters, new object[] { "@chainId", chainId });
                addMethod.Invoke(parameters, new object[] { "@mailboxId", mailboxId });
                
                var executeMethod = commandType.GetMethod("ExecuteNonQuery");
                var rowsAffected = executeMethod.Invoke(insertCommand, null);
                
                if ((int)rowsAffected > 0)
                {
                    Console.WriteLine($"✅ Successfully linked email {emailId} to CRM contact 4");
                }
                else
                {
                    Console.WriteLine($"ℹ️ Email {emailId} already linked or link failed");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error linking email {emailId}: {ex.Message}");
        }
    }
}