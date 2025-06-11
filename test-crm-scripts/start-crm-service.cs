using System;
using System.Reflection;

class StartCrmService
{
    static void Main()
    {
        try
        {
            Console.WriteLine("Starting Enhanced CRM Service...");
            
            var assembly = Assembly.LoadFrom("/var/www/onlyoffice/WebStudio/bin/ASC.Mail.dll");
            var serviceType = assembly.GetType("ASC.Mail.Core.Engine.WebCrmMonitoringService");
            
            if (serviceType \!= null)
            {
                var startMethod = serviceType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                if (startMethod \!= null)
                {
                    startMethod.Invoke(null, null);
                    Console.WriteLine("WebCrmMonitoringService started successfully\!");
                    
                    var statusMethod = serviceType.GetMethod("GetStatus", BindingFlags.Public | BindingFlags.Static);
                    if (statusMethod \!= null)
                    {
                        var status = statusMethod.Invoke(null, null);
                        Console.WriteLine("Service Status: " + status);
                    }
                    
                    var isRunningProperty = serviceType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Static);
                    if (isRunningProperty \!= null)
                    {
                        var isRunning = isRunningProperty.GetValue(null);
                        Console.WriteLine("Is Running: " + isRunning);
                    }
                }
                else
                {
                    Console.WriteLine("Start method not found");
                }
            }
            else
            {
                Console.WriteLine("WebCrmMonitoringService type not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
EOF < /dev/null