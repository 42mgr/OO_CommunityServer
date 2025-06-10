<%@ WebHandler Language="C#" Class="CrmAutoLinkTrigger" %>

using System;
using System.Web;
using System.Reflection;

public class CrmAutoLinkTrigger : IHttpHandler 
{
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
                        // Use reflection to start the CRM Email Auto-Link Service
                        var mailAssembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "ASC.Mail");
                        
                        if (mailAssembly != null)
                        {
                            var serviceType = mailAssembly.GetType("ASC.Mail.Core.Engine.CrmEmailAutoLinkService");
                            if (serviceType != null)
                            {
                                var startMethod = serviceType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                                if (startMethod != null)
                                {
                                    startMethod.Invoke(null, null);
                                    _initialized = true;
                                    
                                    context.Response.ContentType = "text/plain";
                                    context.Response.Write("✅ CRM Email Auto-Link Service started successfully!");
                                    return;
                                }
                            }
                        }
                        
                        context.Response.ContentType = "text/plain";
                        context.Response.Write("❌ Failed to find CrmEmailAutoLinkService");
                    }
                    catch (Exception ex)
                    {
                        context.Response.ContentType = "text/plain";
                        context.Response.Write("❌ Error starting service: " + ex.Message);
                    }
                }
                else
                {
                    context.Response.ContentType = "text/plain";
                    context.Response.Write("ℹ️ CRM Email Auto-Link Service already running");
                }
            }
        }
        else
        {
            context.Response.ContentType = "text/plain";
            context.Response.Write("ℹ️ CRM Email Auto-Link Service already running");
        }
    }
 
    public bool IsReusable 
    {
        get { return false; }
    }
}