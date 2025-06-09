<%@ WebHandler Language="C#" Class="StartupTrigger" %>

using System;
using System.Web;
using ASC.Mail.Enhanced;
using log4net;

public class StartupTrigger : IHttpHandler 
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(StartupTrigger));
    private static bool _initialized = false;
    private static readonly object _lock = new object();
    
    public void ProcessRequest(HttpContext context) 
    {
        if (!_initialized)
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    try
                    {
                        Log.Info("🚀 StartupTrigger: Initializing enhanced CRM auto-linking service...");
                        
                        // Initialize the SimpleCrmAutoLinker
                        SimpleCrmAutoLinker.Initialize();
                        
                        _initialized = true;
                        
                        context.Response.ContentType = "text/plain";
                        context.Response.Write("✅ Enhanced CRM auto-linking service initialized successfully");
                        
                        Log.Info("✅ StartupTrigger: Enhanced CRM auto-linking service started successfully");
                    }
                    catch (Exception ex)
                    {
                        Log.Error("❌ StartupTrigger: Failed to initialize enhanced CRM auto-linking service", ex);
                        context.Response.ContentType = "text/plain";
                        context.Response.Write("❌ Failed to initialize enhanced CRM auto-linking service: " + ex.Message);
                    }
                }
                else
                {
                    context.Response.ContentType = "text/plain";
                    context.Response.Write("ℹ️ Enhanced CRM auto-linking service already running");
                }
            }
        }
        else
        {
            context.Response.ContentType = "text/plain";
            context.Response.Write("ℹ️ Enhanced CRM auto-linking service already running");
        }
    }
 
    public bool IsReusable 
    {
        get { return false; }
    }
}