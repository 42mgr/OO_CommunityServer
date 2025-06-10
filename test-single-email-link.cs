using System;
using System.Reflection;

class TestSingleEmailLink
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔗 Testing single email link for email 5008 (Test 31)...");
            
            // Load the ASC.Mail assembly
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            
            // Try to manually invoke the service processing logic
            var serviceType = assembly.GetType("ASC.Mail.Core.Engine.CrmEmailAutoLinkService");
            
            // Get the private ProcessEmailForCrmAutoLinking method
            var processMethod = serviceType.GetMethod("ProcessEmailForCrmAutoLinking", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            if (processMethod == null)
            {
                Console.WriteLine("❌ ProcessEmailForCrmAutoLinking method not found");
                return;
            }
            
            Console.WriteLine("✅ Found ProcessEmailForCrmAutoLinking method");
            
            // We need to create a MailMessageData object for email 5008
            // Let's see what parameters the method expects
            var parameters = processMethod.GetParameters();
            Console.WriteLine($"Method parameters:");
            foreach (var param in parameters)
            {
                Console.WriteLine($"  - {param.ParameterType.Name} {param.Name}");
            }
            
            // The method needs: (MailMessageData message, int tenantId, string userId)
            // Let's try to create these
            var messageDataType = assembly.GetType("ASC.Mail.Data.Contracts.MailMessageData");
            if (messageDataType == null)
            {
                Console.WriteLine("❌ MailMessageData type not found");
                return;
            }
            
            Console.WriteLine("✅ Found MailMessageData type");
            
            // Create a minimal message object for email 5008
            var message = Activator.CreateInstance(messageDataType);
            
            // Set basic properties
            messageDataType.GetProperty("Id")?.SetValue(message, 5008);
            messageDataType.GetProperty("From")?.SetValue(message, "mgrafde@gmail.com");
            messageDataType.GetProperty("To")?.SetValue(message, "mgrafch@gmail.com");
            
            Console.WriteLine("✅ Created MailMessageData object");
            
            // Try to invoke the method
            Console.WriteLine("🔄 Testing email processing...");
            var result = processMethod.Invoke(null, new object[] { message, 1, "7af56a6a-3617-11f0-9f5a-0242ac130004" });
            
            Console.WriteLine($"✅ Method returned: {result}");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }
}