using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Direct CRM Linking Test
/// Manually links Test 30 and Test 31 emails using reflection to call LinkChainToCrmEnhanced
/// </summary>
class TestDirectCrmLink
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔗 Direct CRM Linking Test for Test 30 and Test 31 emails");
            
            // Load the ASC.Mail assembly
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            var crmLinkEngineType = assembly.GetType("ASC.Mail.Core.Engine.CrmLinkEngine");
            var crmContactDataType = assembly.GetType("ASC.Mail.Data.Contracts.CrmContactData");
            var entityTypesType = assembly.GetType("ASC.Mail.Data.Contracts.CrmContactData+EntityTypes");
            
            if (crmLinkEngineType == null || crmContactDataType == null)
            {
                Console.WriteLine("❌ Required types not found");
                return;
            }
            
            Console.WriteLine("✅ Found required types");
            
            // Create CrmLinkEngine instance (tenant=1, user=admin, log=null)
            // Try different constructor signatures
            var constructor = crmLinkEngineType.GetConstructor(new Type[] { typeof(int), typeof(string) });
            if (constructor == null)
            {
                // Try with ILog parameter
                var logType = assembly.GetType("ASC.Common.Logging.ILog");
                constructor = crmLinkEngineType.GetConstructor(new Type[] { typeof(int), typeof(string), logType });
                if (constructor == null)
                {
                    Console.WriteLine("❌ Constructor not found");
                    return;
                }
            }
            
            // Invoke constructor with appropriate parameters
            object crmEngine;
            var parameters = constructor.GetParameters();
            if (parameters.Length == 2)
            {
                crmEngine = constructor.Invoke(new object[] { 1, "7af56a6a-3617-11f0-9f5a-0242ac130004" });
            }
            else
            {
                crmEngine = constructor.Invoke(new object[] { 1, "7af56a6a-3617-11f0-9f5a-0242ac130004", null });
            }
            Console.WriteLine("✅ Created CrmLinkEngine instance");
            
            // Create contact data list for contact ID 4 (MarcelM!=!GGGRaf with mgrafde@gmail.com)
            var contactsList = Activator.CreateInstance(typeof(List<>).MakeGenericType(crmContactDataType));
            var contactData = Activator.CreateInstance(crmContactDataType);
            
            // Set contact ID = 4 and Type = Contact (1)
            crmContactDataType.GetProperty("Id").SetValue(contactData, 4);
            crmContactDataType.GetProperty("Type").SetValue(contactData, 1); // EntityTypes.Contact
            
            // Add to list
            var addMethod = contactsList.GetType().GetMethod("Add");
            addMethod.Invoke(contactsList, new object[] { contactData });
            
            Console.WriteLine("✅ Created contact data for contact ID 4");
            
            // Get LinkChainToCrmEnhanced method
            var linkMethod = crmLinkEngineType.GetMethod("LinkChainToCrmEnhanced");
            if (linkMethod == null)
            {
                Console.WriteLine("❌ LinkChainToCrmEnhanced method not found");
                return;
            }
            
            Console.WriteLine("✅ Found LinkChainToCrmEnhanced method");
            
            // Link Test 30 and Test 31 emails (IDs: 5006, 5007, 5008, 5009)
            var emailIds = new int[] { 5006, 5007, 5008, 5009 };
            var emailNames = new string[] { "Test 30 (inbox)", "Test 30 (sent)", "Test 31 (inbox)", "Test 31 (sent)" };
            
            for (int i = 0; i < emailIds.Length; i++)
            {
                try
                {
                    Console.WriteLine($"🔗 Linking email {emailIds[i]} ({emailNames[i]})...");
                    linkMethod.Invoke(crmEngine, new object[] { emailIds[i], contactsList, "http" });
                    Console.WriteLine($"✅ Successfully linked email {emailIds[i]}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error linking email {emailIds[i]}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                    }
                }
            }
            
            Console.WriteLine("\n🎉 Direct CRM linking test completed!");
            Console.WriteLine("📋 Check the database to verify emails are now linked to CRM contact 4");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}