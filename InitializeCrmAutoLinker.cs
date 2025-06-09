using System;
using System.Web;
using ASC.Mail.Enhanced;
using ASC.Common.Logging;
using log4net;

namespace ASC.Web.Studio
{
    /// <summary>
    /// Initialize the enhanced CRM auto-linking service on application start
    /// </summary>
    public class CrmAutoLinkerInitializer : IHttpModule
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CrmAutoLinkerInitializer));
        private static bool _initialized = false;
        private static readonly object _lock = new object();
        
        public void Init(HttpApplication context)
        {
            if (!_initialized)
            {
                lock (_lock)
                {
                    if (!_initialized)
                    {
                        try
                        {
                            Log.Info("🚀 CrmAutoLinkerInitializer: Starting enhanced CRM auto-linking service...");
                            
                            // Initialize the SimpleCrmAutoLinker
                            SimpleCrmAutoLinker.Initialize();
                            
                            _initialized = true;
                            Log.Info("✅ CrmAutoLinkerInitializer: Enhanced CRM auto-linking service initialized successfully");
                        }
                        catch (Exception ex)
                        {
                            Log.Error("❌ CrmAutoLinkerInitializer: Failed to initialize enhanced CRM auto-linking service", ex);
                        }
                    }
                }
            }
        }
        
        public void Dispose()
        {
            try
            {
                SimpleCrmAutoLinker.Stop();
                Log.Info("🛑 CrmAutoLinkerInitializer: Enhanced CRM auto-linking service stopped");
            }
            catch (Exception ex)
            {
                Log.Error("❌ CrmAutoLinkerInitializer: Error stopping enhanced CRM auto-linking service", ex);
            }
        }
    }
}