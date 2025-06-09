<%@ WebHandler Language="C#" Class="TestTrigger" %>

using System;
using System.Web;

public class TestTrigger : IHttpHandler 
{
    public void ProcessRequest(HttpContext context) 
    {
        try
        {
            string result = TestCrmAutoLink.ProcessRecentEmails();
            
            context.Response.ContentType = "text/plain";
            context.Response.Write(result);
        }
        catch (Exception ex)
        {
            context.Response.ContentType = "text/plain";
            context.Response.Write("❌ Error: " + ex.Message);
        }
    }
 
    public bool IsReusable 
    {
        get { return false; }
    }
}