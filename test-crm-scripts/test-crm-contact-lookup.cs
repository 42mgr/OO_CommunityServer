using System;
using System.Reflection;

class TestCrmContactLookup
{
    static void Main()
    {
        try
        {
            Console.WriteLine("🔍 Testing CRM contact lookup for mgrafde@gmail.com...");
            
            // Load assemblies
            var mailAssembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            
            // Get DaoFactory
            var daoFactoryType = mailAssembly.GetType("ASC.Mail.Core.Dao.DaoFactory");
            if (daoFactoryType == null)
            {
                Console.WriteLine("❌ DaoFactory type not found");
                return;
            }
            
            Console.WriteLine("✅ Found DaoFactory type");
            
            // Create DaoFactory instance
            var daoFactory = Activator.CreateInstance(daoFactoryType);
            
            // Get CreateCrmContactDao method
            var createCrmContactDaoMethod = daoFactoryType.GetMethod("CreateCrmContactDao", new Type[] { typeof(int), typeof(string) });
            if (createCrmContactDaoMethod == null)
            {
                Console.WriteLine("❌ CreateCrmContactDao method not found");
                return;
            }
            
            Console.WriteLine("✅ Found CreateCrmContactDao method");
            
            // Create CRM contact DAO
            var crmContactDao = createCrmContactDaoMethod.Invoke(daoFactory, new object[] { 1, "7af56a6a-3617-11f0-9f5a-0242ac130004" });
            
            Console.WriteLine("✅ Created CrmContactDao");
            
            // Get GetCrmContactIds method
            var getCrmContactIdsMethod = crmContactDao.GetType().GetMethod("GetCrmContactIds", new Type[] { typeof(string) });
            if (getCrmContactIdsMethod == null)
            {
                Console.WriteLine("❌ GetCrmContactIds method not found");
                return;
            }
            
            Console.WriteLine("✅ Found GetCrmContactIds method");
            
            // Test lookup for mgrafde@gmail.com
            var contactIds = getCrmContactIdsMethod.Invoke(crmContactDao, new object[] { "mgrafde@gmail.com" });
            
            Console.WriteLine($"✅ CRM contact lookup result: {contactIds}");
            
            // Check if it's a collection and get count
            if (contactIds != null)
            {
                var countProperty = contactIds.GetType().GetProperty("Count");
                if (countProperty != null)
                {
                    var count = countProperty.GetValue(contactIds);
                    Console.WriteLine($"Found {count} CRM contacts for mgrafde@gmail.com");
                }
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}