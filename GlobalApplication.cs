using System;
using System.Web;
using ASC.Mail.Enhanced;
using log4net;

/// <summary>
/// Enhanced Global Application class that initializes CRM auto-linking
/// </summary>
public class EnhancedGlobal : HttpApplication
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(EnhancedGlobal));
    
    protected void Application_Start()
    {
        try
        {
            Log.Info("🚀 EnhancedGlobal: Application starting - initializing enhanced CRM auto-linking...");
            
            // Initialize the SimpleCrmAutoLinker
            SimpleCrmAutoLinker.Initialize();
            
            Log.Info("✅ EnhancedGlobal: Enhanced CRM auto-linking service started successfully");
        }
        catch (Exception ex)
        {
            Log.Error("❌ EnhancedGlobal: Failed to initialize enhanced CRM auto-linking service", ex);
        }
    }
    
    protected void Application_End()
    {
        try
        {
            SimpleCrmAutoLinker.Stop();
            Log.Info("🛑 EnhancedGlobal: Enhanced CRM auto-linking service stopped");
        }
        catch (Exception ex)
        {
            Log.Error("❌ EnhancedGlobal: Error stopping enhanced CRM auto-linking service", ex);
        }
    }
}