using System;
using System.Reflection;
using System.IO;

// Simple test to verify our enhanced CRM functionality can be loaded
public class TestEnhancedCRM
{
    public static void Main(string[] args)
    {
        try 
        {
            Console.WriteLine("Testing enhanced CRM loading...");
            
            // Load our enhanced ASC.Mail.dll
            string enhancedPath = "/var/www/onlyoffice/Services/MailAggregator/ASC.Mail.Enhanced.dll";
            if (File.Exists(enhancedPath))
            {
                Assembly enhancedAssembly = Assembly.LoadFrom(enhancedPath);
                Console.WriteLine($"Successfully loaded enhanced assembly: {enhancedAssembly.FullName}");
                
                // Look for CrmLinkEngine
                Type crmEngineType = enhancedAssembly.GetType("ASC.Mail.Core.Engine.CrmLinkEngine");
                if (crmEngineType != null)
                {
                    Console.WriteLine("Found CrmLinkEngine in enhanced assembly");
                    
                    // Check for our enhanced method
                    MethodInfo enhancedMethod = crmEngineType.GetMethod("LinkChainToCrmEnhanced");
                    if (enhancedMethod != null)
                    {
                        Console.WriteLine("SUCCESS: Found enhanced LinkChainToCrmEnhanced method!");
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Enhanced method not found");
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: CrmLinkEngine not found in enhanced assembly");
                }
            }
            else
            {
                Console.WriteLine($"ERROR: Enhanced assembly not found at {enhancedPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}