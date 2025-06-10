using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

class ManualLinkTest29
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔗 Manually linking Test 29 emails to CRM...");
            
            // Load the ASC.Mail assembly
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            
            // Get the CrmLinkEngine type
            var crmLinkEngineType = assembly.GetType("ASC.Mail.Core.Engine.CrmLinkEngine");
            if (crmLinkEngineType == null)
            {
                Console.WriteLine("❌ CrmLinkEngine type not found");
                return;
            }
            
            Console.WriteLine("✅ Found CrmLinkEngine type");
            
            // We need to create an instance with tenant and user
            // Based on the emails, tenant = 1, and we need to find the user
            int tenantId = 1;
            string userId = "00000000-0000-0000-0000-000000000000"; // Default admin user
            
            // Create CrmLinkEngine instance
            object crmEngine;
            
            // Try with ILog parameter first (from API info)
            var logType = assembly.GetType("ASC.Common.Logging.ILog");
            var constructor = crmLinkEngineType.GetConstructor(new Type[] { typeof(int), typeof(string), logType });
            if (constructor != null)
            {
                crmEngine = constructor.Invoke(new object[] { tenantId, userId, null });
            }
            else
            {
                // Try without ILog parameter
                constructor = crmLinkEngineType.GetConstructor(new Type[] { typeof(int), typeof(string) });
                if (constructor == null)
                {
                    Console.WriteLine("❌ Constructor not found");
                    return;
                }
                crmEngine = constructor.Invoke(new object[] { tenantId, userId });
            }
            
            Console.WriteLine("✅ Created CrmLinkEngine instance");
            
            // Get the LinkChainToCrmEnhanced method
            var linkMethod = crmLinkEngineType.GetMethod("LinkChainToCrmEnhanced");
            if (linkMethod == null)
            {
                Console.WriteLine("❌ LinkChainToCrmEnhanced method not found");
                return;
            }
            
            Console.WriteLine("✅ Found LinkChainToCrmEnhanced method");
            
            // Try to link email 5002 and 5003 to contact 4
            var contactDataType = assembly.GetType("ASC.Mail.Data.Contracts.CrmContactData");
            var entityTypesType = assembly.GetType("ASC.Mail.Data.Contracts.CrmContactData+EntityTypes");
            
            // Create contact data list
            var contactsList = Activator.CreateInstance(typeof(List<>).MakeGenericType(contactDataType));
            var contactData = Activator.CreateInstance(contactDataType);
            
            // Set contact ID = 4 and Type = Contact (1)
            contactDataType.GetProperty("Id").SetValue(contactData, 4);
            contactDataType.GetProperty("Type").SetValue(contactData, 1); // EntityTypes.Contact
            
            // Add to list
            var addMethod = contactsList.GetType().GetMethod("Add");
            addMethod.Invoke(contactsList, new object[] { contactData });
            
            Console.WriteLine("✅ Created contact data for contact ID 4");
            
            // Link email 5002
            Console.WriteLine("🔗 Linking email 5002...");
            linkMethod.Invoke(crmEngine, new object[] { 5002, contactsList, "http" });
            Console.WriteLine("✅ Linked email 5002");
            
            // Link email 5003  
            Console.WriteLine("🔗 Linking email 5003...");
            linkMethod.Invoke(crmEngine, new object[] { 5003, contactsList, "http" });
            Console.WriteLine("✅ Linked email 5003");
            
            Console.WriteLine("🎉 Successfully linked Test 29 emails to CRM contact 4!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}