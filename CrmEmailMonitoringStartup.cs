using System;
using System.Web;
using log4net;

namespace ASC.Mail.Enhanced
{
    /// <summary>
    /// HTTP Module to automatically start CRM Email Monitoring when the web application starts
    /// Add this to web.config in the system.web/httpModules section
    /// </summary>
    public class CrmEmailMonitoringStartupModule : IHttpModule
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CrmEmailMonitoringStartupModule));
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();
        
        public void Init(HttpApplication context)
        {
            lock (_lockObject)
            {
                if (_isInitialized) return;
                
                try
                {
                    Log.Info("🚀 CrmEmailMonitoringStartupModule: Initializing CRM Email Monitoring via HTTP Module...");
                    CrmEmailMonitoringInitializer.Initialize();
                    _isInitialized = true;
                    Log.Info("✅ CrmEmailMonitoringStartupModule: CRM Email Monitoring initialization complete");
                }
                catch (Exception ex)
                {
                    Log.Error("❌ CrmEmailMonitoringStartupModule: Error initializing CRM Email Monitoring", ex);
                }
            }
        }
        
        public void Dispose()
        {
            try
            {
                Log.Info("🛑 CrmEmailMonitoringStartupModule: Shutting down CRM Email Monitoring...");
                CrmEmailMonitoringInitializer.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error("❌ CrmEmailMonitoringStartupModule: Error during shutdown", ex);
            }
        }
    }
    
    /// <summary>
    /// Global.asax.cs code snippet for manual integration
    /// Copy the methods below into your Global.asax.cs file
    /// </summary>
    public static class GlobalAsaxIntegration
    {
        // Add this to Global.asax.cs Application_Start method:
        public static void Application_Start_AddThis()
        {
            /*
            // Add this code to your Global.asax.cs Application_Start method:
            try
            {
                Log.Info("🚀 Global.asax: Starting CRM Email Monitoring...");
                CrmEmailMonitoringInitializer.Initialize();
                Log.Info("✅ Global.asax: CRM Email Monitoring started successfully");
            }
            catch (Exception ex)
            {
                Log.Error("❌ Global.asax: Error starting CRM Email Monitoring", ex);
            }
            */
        }
        
        // Add this to Global.asax.cs Application_End method:
        public static void Application_End_AddThis()
        {
            /*
            // Add this code to your Global.asax.cs Application_End method:
            try
            {
                Log.Info("🛑 Global.asax: Stopping CRM Email Monitoring...");
                CrmEmailMonitoringInitializer.Shutdown();
                Log.Info("✅ Global.asax: CRM Email Monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                Log.Error("❌ Global.asax: Error stopping CRM Email Monitoring", ex);
            }
            */
        }
    }
}