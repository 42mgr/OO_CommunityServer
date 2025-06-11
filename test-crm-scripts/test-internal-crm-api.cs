using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Test Internal CRM API
/// Makes HTTP requests to the new CRM auto-link API endpoints from within OnlyOffice
/// </summary>
class TestInternalCrmApi
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("🔗 Testing Internal CRM API");
            Console.WriteLine("============================");
            
            // Test the API endpoints
            RunTests().Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
    
    static async Task RunTests()
    {
        var baseUrl = "http://localhost:1180";
        
        using (var client = new HttpClient())
        {
            // Add authentication headers if needed
            client.DefaultRequestHeaders.Add("User-Agent", "OnlyOffice-CRM-AutoLink-Test");
            
            Console.WriteLine("🧪 Test 1: Check CRM Auto-Link Status");
            await TestApiEndpoint(client, $"{baseUrl}/api/2.0/mail/crm/autolink/status");
            
            Console.WriteLine("\n🧪 Test 2: Test Enhanced CRM Functionality");
            await TestApiEndpoint(client, $"{baseUrl}/api/2.0/mail/crm/autolink/test", "POST");
            
            Console.WriteLine("\n🧪 Test 3: Link Specific Email (5006)");
            await TestApiEndpoint(client, $"{baseUrl}/api/2.0/mail/crm/autolink/5006", "POST");
            
            Console.WriteLine("\n🧪 Test 4: Process Recent Emails");
            await TestApiEndpoint(client, $"{baseUrl}/api/2.0/mail/crm/autolink/recent", "POST");
        }
    }
    
    static async Task TestApiEndpoint(HttpClient client, string url, string method = "GET")
    {
        try
        {
            Console.WriteLine($"📡 {method} {url}");
            
            HttpResponseMessage response;
            if (method == "POST")
            {
                response = await client.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"));
            }
            else
            {
                response = await client.GetAsync(url);
            }
            
            var content = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Response: {content.Substring(0, Math.Min(content.Length, 500))}");
            
            if (content.Length > 500)
            {
                Console.WriteLine("... (truncated)");
            }
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✅ Success");
            }
            else
            {
                Console.WriteLine("⚠️ Non-success status");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}